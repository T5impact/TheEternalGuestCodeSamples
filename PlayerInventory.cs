using Attacks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using UnityEngine.InputSystem;

[System.Serializable]
public class PlayerInventory : MonoBehaviour
{
    //Player inventory script that allows the storing of
    //4 different object types: weapon, item, memento, ability
    //Stores each type in a different data structure and keeps track
    //of current equipped one for each
    //Supports loading from save file
    //Integrates seamlessly into quest system

    public int emotionalEnergy { get; private set; }
    public int emotionalEnergyGained { get; set; }

    Weapon[] weaponsInventory;
    Item[] itemsInventory;
    List<MementoInfo> mementosInventory;
    List<Ability> abilitiesInventory;

    //Currently equipped weapon/item/ability/memento index and reference
    public int currentWeaponIndex { get; private set; }
    public int currentItemIndex { get; private set; }
    public int currentAbilityIndex { get; private set; }
    public int currentMementoIndex { get; private set; }
    public Weapon CurrentWeapon { get => GetWeapon(currentWeaponIndex); }
    public Item CurrentItem { get => GetItem(currentItemIndex); }
    public Ability CurrentAbility { get => GetAbility(currentAbilityIndex); }
    public MementoInfo CurrentMemento { get => GetMemento(currentMementoIndex); }

    [SerializeField] ItemInfo[] possibleItemInfos;
    [SerializeField] WeaponInfo[] possibleWeaponInfos;
    [SerializeField] AbilityInfo[] possibleAbilityInfos;


    [Header("References")]
    [SerializeField] int itemInventorySize = 12;
    [SerializeField] int weaponInventorySize = 12;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] PlayerGUI playerGUI;
    [SerializeField] NewAudioManager audioManager;

    ActionController actionController;

    bool inventoryLoaded = false;
    public event EventHandler OnItemCollect; //Inventory events for quest system
    public event EventHandler OnEnergyCollect;

    //Initalize all of the inventories and
    //establish the item database for all of the weapons/items/abilities
    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        actionController = GetComponent<ActionController>();

        UsableDatabase.itemInfos = new ItemInfo[possibleItemInfos.Length];
        UsableDatabase.abilityInfos = new AbilityInfo[possibleAbilityInfos.Length];
        UsableDatabase.weaponInfos = new WeaponInfo[possibleWeaponInfos.Length];

        for (int i = 0; i < possibleItemInfos.Length; i++)
        {
            UsableDatabase.itemInfos[i] = possibleItemInfos[i];
            UsableDatabase.itemInfos[i].SetID(i);
        }
        for (int i = 0; i < possibleAbilityInfos.Length; i++)
        {
            UsableDatabase.abilityInfos[i] = possibleAbilityInfos[i];
            UsableDatabase.abilityInfos[i].SetID(i);
        }
        for (int i = 0; i < possibleWeaponInfos.Length; i++)
        {
            UsableDatabase.weaponInfos[i] = possibleWeaponInfos[i];
            UsableDatabase.weaponInfos[i].SetID(i);
        }

        if (!inventoryLoaded)
        {
            currentWeaponIndex = -1;
            currentItemIndex = -1;
            currentAbilityIndex = -1;
            currentMementoIndex = -1;

            itemsInventory = new Item[itemInventorySize];
            weaponsInventory = new Weapon[weaponInventorySize];

            abilitiesInventory = new List<Ability>();
            mementosInventory = new List<MementoInfo>();
        }
    }

    //Detect if player picks up item/weapon/memento using inheritance
    //Connect inventory events to quest system
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ItemData data = collision.GetComponent<ItemData>();

        if (data)
        {
            bool remove = true;
            UseableInfo useable = data.GetItemData();
            int count = data.GetCount();
            OnItemCollect?.Invoke(useable.GetName(), EventArgs.Empty);
            audioManager.PlayEffect("ItemPickup");
            if (useable.GetType() == typeof(ItemInfo))
            {
                Item newItem = new Item((ItemInfo)useable, count);
                remove = AddItem(newItem);
            }
            else if (useable.GetType() == typeof(MementoInfo))
            {
                MementoData mementoData = (MementoData)data;
                remove = AddMemento((MementoInfo)useable);
                OnEnergyCollect?.Invoke(mementoData.eeWorth.ToString(), EventArgs.Empty);
                AddEmotionalEnergy(mementoData.eeWorth);
            }
            else if (useable.GetType() == typeof(WeaponInfo))
            {
                remove = AddWeapon(new Weapon((WeaponInfo)useable));
            }
            if(remove) Destroy(collision.gameObject);
        } 
        else
        {
            EmotionalEnergy energy = collision.GetComponent<EmotionalEnergy>();
            if(energy)
            {
                OnEnergyCollect?.Invoke(energy.emotionalEnergy.ToString(), EventArgs.Empty);
                AddEmotionalEnergy(energy.emotionalEnergy);
                Destroy(collision.gameObject);
            }
        }
    }

    //Get specific helper functions
    public Weapon GetWeapon(int index)
    {
        return index >= 0 && (index < weaponsInventory.Length && index >= 0) ? weaponsInventory[index] : null;
    }
    public Item GetItem(int index)
    {
        return index >= 0 && (index < itemsInventory.Length && index >= 0) ? itemsInventory[index] : null;
    }
    public Ability GetAbility(int index)
    {
        return index >= 0 && (index < abilitiesInventory.Count && index >= 0) ? abilitiesInventory[index] : null;
    }
    public MementoInfo GetMemento(int index)
    {
        return index >= 0 && (index < mementosInventory.Count && index >= 0) ? mementosInventory[index] : null;
    }

    #region Equipping From Inventory
    public void EquipWeapon(int index)
    {
        Weapon weapon = GetWeapon(index);

        if (weapon != null || index < 0)
        {
            currentWeaponIndex = index;
        }

        actionController.EquipWeapon(weapon);
    }
    public void EquipItem(int index)
    {
        Item item = GetItem(index);

        if (item != null || index < 0)
        {
            currentItemIndex = index;
            currentAbilityIndex = -1;

            actionController.UnequipPlayerAbility();
        }
    }
    public void EquipPlayerAbility(int index)
    {
        Ability ability = index >= 0 && index < abilitiesInventory.Count ? abilitiesInventory[index] : null;

        if (ability != null || index < 0)
        {
            currentAbilityIndex = index;
            currentItemIndex = -1;
            actionController.EquipPlayerAbility(ability);
        }
        else
        {
            actionController.UnequipPlayerAbility();
        }
    }

    public void EquipMemento(int index)
    {
        MementoInfo mementos = index < mementosInventory.Count ? mementosInventory[index] : null;

        if (mementos != null)
        {
            currentMementoIndex = index;
            actionController.EquipSpecialAbility(mementos);
        }
    }
    #endregion

    #region Adding to Inventory
    public bool AddItem(Item item)
    {
        print("adding item: " + item.GetInfo().GetName());
        for (int i = 0; i < itemsInventory.Length; i++)
        {
            if (item != null && itemsInventory[i] != null && itemsInventory[i].GetInfo().GetName() == item.GetInfo().GetName())
            {
                int newAmount = itemsInventory[i].AddAmount(item.CurrentAmount);
                if (newAmount > 0)
                {
                    item.SetCurrentAmount(newAmount);
                    continue;
                }
                return true;
            }
        }

        for (int i = 0; i < itemsInventory.Length; i++)
        {
            if (item != null && itemsInventory[i] == null)
            {
                itemsInventory[i] = item;
                return true;
            } 
        }
        return false;
    }
    public bool AddWeapon(Weapon weapon)
    {
        for (int i = 0; i < weaponsInventory.Length; i++)
        {
            if (weapon != null && weaponsInventory[i] == null)
            {
                weaponsInventory[i] = weapon;
                return true;
            }
        }
        return false;
    }
    public bool AddMemento(MementoInfo memento)
    {
        if(memento != null && !mementosInventory.Contains(memento))
        {
            mementosInventory.Add(memento);
            print("Added " + memento.GetName() + "- Count: " + mementosInventory.Count);
            return true;
        } else
        {
            return false;
        }
    }
    public int AddPlayerAbility(Ability ability)
    {
        if (ability != null && !abilitiesInventory.Contains(ability))
        {
            abilitiesInventory.Add(ability);
            return abilitiesInventory.Count - 1;
        }
        else
        {
            return -1;
        }
    }
    #endregion

    //Remove functions for a specific index
    public void RemoveWeapon(int index)
    {
        Weapon weapon = GetWeapon(index);
        if(weapon != null)
        {
            if(index == currentWeaponIndex)
            {
                EquipWeapon(-1);
            }

            weaponsInventory[index] = null;
        }
    }
    public void RemoveItem(int index)
    {
        Item item = GetItem(index);
        if (item != null)
        {
            if (index == currentItemIndex)
            {
                EquipItem(-1);
            }

            itemsInventory[index] = null;
        }
    }


    public void UseItem(int index)
    {
        Item item = GetItem(index);
        if (item != null)
        {
            item.RemoveAmount(1);

            if(item.CurrentAmount <= 0)
            {
                itemsInventory[index] = null;

                if (index == currentItemIndex)
                    currentItemIndex = -1;
            }
        }
    }

    public int HasAbility(string abilityName)
    {
        for (int i = 0; i < abilitiesInventory.Count; i++)
        {
            if (abilitiesInventory[i].GetInfo().GetName().Equals(abilityName)) return i;
        }
        return -1;
    }

    //Get all helper functions
    public Weapon[] GetWeapons()
    {
        return weaponsInventory;
    }
    public Item[] GetItems()
    {
        return itemsInventory;
    }
    public List<MementoInfo> GetMementos()
    {
        return mementosInventory;
    }
    public List<Ability> GetAbilities()
    {
        return abilitiesInventory;
    }

    /// <summary>
    /// Adds amount to emotional energy counter
    /// </summary>
    /// <param name="amount"></param>
    public void AddEmotionalEnergy(int amount)
    {
        emotionalEnergy += amount;
        Floor floor = FindObjectOfType<Floor>();
        floor.AddEmotionalEnergy(amount);
        playerGUI.UpdateEmotionalEnergy();
    }
    /// <summary>
    /// Subtracts amount from emotional energy counter and returns whether it was successful
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool SubtractEmotionialEnergy(int amount)
    {
        if (emotionalEnergy < amount) return false;
        emotionalEnergy -= amount;

        playerGUI.UpdateEmotionalEnergy();
        return true;
    }


    //Load inventory from JSON save file
    public bool LoadInventory(SerializedClass serializedClass)
    {
        if(serializedClass == null)
        {
            Debug.LogError("Cannot load inventory, given serialized class is null.");
            return false;
        }

        itemsInventory = new Item[itemInventorySize];
        weaponsInventory = new Weapon[weaponInventorySize];
        abilitiesInventory = new List<Ability>();
        mementosInventory = new List<MementoInfo>();

        emotionalEnergy = 0;
        AddEmotionalEnergy(serializedClass.emotionalEnergy);

        emotionalEnergyGained = serializedClass.emotionalEnergyGained;


        currentItemIndex = serializedClass.currentItemIndex;
        currentWeaponIndex = serializedClass.currentWeaponIndex;
        currentAbilityIndex = serializedClass.currentAbilityIndex;
        currentMementoIndex = serializedClass.currentMementoIndex;

        for (int i = 0; i < serializedClass.bobs.Length; i++)
        {
            if (serializedClass.bobs[i].id != -1)
                AddWeapon(new Weapon(serializedClass.bobs[i]));
        }
        for (int i = 0; i < serializedClass.joes.Length; i++)
        {
            if (serializedClass.joes[i].id != -1)
                AddItem(new Item(serializedClass.joes[i]));
        }
        for (int i = 0; i < serializedClass.bills.Length; i++)
        {
            if (serializedClass.bills[i].id != -1)
                AddPlayerAbility(new Ability(serializedClass.bills[i]));
        }
        for (int i = 0; i < serializedClass.mementosInventory.Count; i++)
        {
            AddMemento(serializedClass.mementosInventory[i]);
        }

        if (currentItemIndex >= 0)
            EquipItem(currentItemIndex);
        if (currentWeaponIndex >= 0)
            EquipWeapon(currentWeaponIndex);
        if (currentAbilityIndex >= 0)
            EquipPlayerAbility(currentAbilityIndex);
        if (currentMementoIndex >= 0)
            EquipMemento(currentMementoIndex);

        playerGUI.UpdateHotbarGUI();
        inventoryLoaded = true;

        return true;
    }
}
