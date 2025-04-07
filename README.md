# Code Samples for The Eternal Guest

The Eternal Guest is a 2D hack-and-slash narrative game that takes place in a shifting hotel filled with enemies and bosses alike.

Below are some code samples for some of the biggest systems I created for the game.

When creating this game, I always prioritized good code architecture and created many graphs outlining connections between the many complex systems in the game.

---

## AI Script C#

This script is a custom navigation system that utilizes context-based steering and is the basis for enemy and friendly AI.

The system works by shooting a ring of raycasts with weights associated with them. The dot product of the ray direction and the direction to the target is used as the base weight. From there, weights are reduced based on their closeness to obstacles which is propagated to nearby weights. Other contributions will either add or decrease weights, and the AI system moves the object in the direction of the highest weight.

Implements two movement modes. The Direct mode moves the object directly towards the target. The Orbit mode moves the object within a certain range and then applies a shaping function to the weights to circle around the target.

Designed for ease of use by allowing target based on transform and target based on position.

## Player Inventory Script C#

This script handles the player inventory with four different inventory types.

## Serialization Manager Script C#
