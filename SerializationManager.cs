using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;

public class SerializationManager : MonoBehaviour
{
    //Save manager that saves the game state to a JSON file
    //Allows for multiple save files

    PlayerInventory inventory;
    Floor currentFloorObject;
    QuestSystem questSystem;

    QuestSave[] questSaves;

    float playTime;
    float startTime;

    private void Start()
    {
        inventory = FindObjectOfType<PlayerInventory>();
        currentFloorObject = FindObjectOfType<Floor>();
        questSystem = FindObjectOfType<QuestSystem>();
        questSaves = new QuestSave[6];
        for (int i = 0; i < questSaves.Length; i++)
        {
            questSaves[i] = new QuestSave();
            questSaves[i].floorNum = i + 1;
            questSaves[i].objectiveNum = 0;
        }
        startTime = Time.unscaledTime;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "Main Menu" && sceneName != "Death Screen")
            StartCoroutine(StartLoading());
    }

    //Saves game state to the last used save file
    public void SaveData()
    {
        int fileNum = 1;
        if (PlayerPrefs.HasKey("SaveFile"))
            fileNum = PlayerPrefs.GetInt("SaveFile");
        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        //clear the file before writing to it
        File.Delete(saveFile);

        if(inventory == null)
        {
            print("Inventory not found...exiting");
            return;
        }

        //store all relevant game data in one serializable class
        SerializedClass classToSave = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime + (Time.unscaledTime - startTime), fileNum);

        string json = JsonUtility.ToJson(classToSave);

        File.WriteAllText(saveFile, json);
    }
    //Saves game state to the specified save file number
    public void SaveData(int fileNum)
    {
        PlayerPrefs.SetInt("SaveFile", fileNum);
        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        //clear the file before writing to it
        File.Delete(saveFile);

        if (inventory == null)
        {
            print("Inventory not found...exiting");
            return;
        }

        //store all relevant game data in one serializable class
        SerializedClass classToSave = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime + (Time.unscaledTime - startTime), fileNum);

        string json = JsonUtility.ToJson(classToSave);

        File.WriteAllText(saveFile, json);
    }

    //Loads the last used save file
    public void LoadData()
    {
        int fileNum = 1;
        if (PlayerPrefs.HasKey("SaveFile"))
            fileNum = PlayerPrefs.GetInt("SaveFile");
        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        //check if file path exists
        if (!File.Exists(saveFile))
        {
            print("File not found");
            return;
        }

        string json = File.ReadAllText(saveFile);

        SerializedClass classToLoad = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime, fileNum);

        //read the data from file and set jsondata
        JsonUtility.FromJsonOverwrite(json, classToLoad);

        questSaves = classToLoad.questSaves;
        playTime = classToLoad.playTime;

        classToLoad.OverWriteData(inventory, currentFloorObject, questSystem);

        print($"Loaded Save File {fileNum}");
    }
    //Loads the save file specified by the provided save file number
    public void LoadData(int fileNum)
    {
        PlayerPrefs.SetInt("SaveFile", fileNum);

        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        //check if file path exists
        if (!File.Exists(saveFile))
        {
            print("File not found");
            return;
        }

        string json = File.ReadAllText(saveFile);

        SerializedClass classToLoad = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime, fileNum);

        //read the data from file and set jsondata
        JsonUtility.FromJsonOverwrite(json, classToLoad);

        questSaves = classToLoad.questSaves;
        playTime = classToLoad.playTime;

        classToLoad.OverWriteData(inventory, currentFloorObject, questSystem);

        print($"Loaded Save File {fileNum}");
    }
    //Loads the save file specified but without loading the inventory
    public SerializedClass SoftLoadData(int fileNum)
    {
        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        //check if file path exists
        if (!File.Exists(saveFile))
        {
            print("File not found");
            return null;
        }

        string json = File.ReadAllText(saveFile);

        SerializedClass classToLoad = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime, fileNum);

        //read the data from file and set jsondata
        JsonUtility.FromJsonOverwrite(json, classToLoad);

        questSaves = classToLoad.questSaves;
        playTime = classToLoad.playTime;

        return classToLoad;
    }

    //Returns all of the save files for display in the main menu
    public SerializedClass[] GetAllSaveData()
    {
        List<SerializedClass> saves = new List<SerializedClass>();
        foreach(string saveFile in Directory.EnumerateFiles(Application.persistentDataPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            string json = File.ReadAllText(saveFile);
            SerializedClass classToLoad = new SerializedClass(inventory, currentFloorObject, questSaves, questSystem, playTime, 0);
            JsonUtility.FromJsonOverwrite(json, classToLoad);
            saves.Add(classToLoad);
        }
        return saves.ToArray();
    }

    //Deletes a specified save file
    public void DeleteSave(int fileNum)
    {
        string saveFile = $"{Application.persistentDataPath}/saveinformation{fileNum}.json";

        File.Delete(saveFile);
    }

    private void Update()
    {
        #if UNITY_EDITOR
        if(Input.GetKey(KeyCode.RightShift) && Input.GetKey(KeyCode.S))
        {
            print("Saving...");
            SaveData();
        }
        if (Input.GetKey(KeyCode.RightShift) && Input.GetKey(KeyCode.L))
        {
            print("Loading...");
            LoadData();
        }
        #endif
    }

    //Method used to load the data at the start of the game
    //I used a coroutine because the game didn't like doing it exactly at the start, so i gave it a slight delay
    IEnumerator StartLoading()
    {
        yield return null;
        LoadData();
    }
}
