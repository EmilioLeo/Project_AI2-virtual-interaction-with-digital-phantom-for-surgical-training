# Project_AI2-virtual-interaction-with-digital-phantom-for-surgical-training

---

# BaseLine
## Working on Unity and WeArt SDK
---

- Starting to deep the **documentation of WeArt/Unity**
- We should develop the assembling/disassembling of phantom such that it should be **touchable in each its part** starting from input from board or click of mouse. 

# Upgrade part of project
## Propose a possible surgical intervent
---
- Studying a possible intervent to simulate it adding 3D objects
- Inserting Banners during the simulation to **create an easy 
guide to resolve surgical treatment for students**

# WeArt steps
---
1. it is needed to use **WeArtTouchableObject** to obtain a touchable effects on objects that interact with the haptic interfaces
    1. in this case the gameobject should have a collider and a rigidbody
2. it is possible to apply **WeArtHapticObject** to render the effective interaction between haptic devices and object
    2. in this case we have to insert a collider on object  with isTrigger=true. we can track the displacement and the rotation of object wrt the oculus using **WeArtThimbleTrackingObject** and **WeArtDeviceTrackingObject**.
3. We can handle all properties of Hands with **WeArtHandController** which communicate also **WeArtHandGraspingSystem** and **WeArtHandSurfaceExploration**. The last component is useful to explore the surface of **WeArtTouchableObject**.
4. We can interact with the object saving their poses and rotations and at the same time the pose adn rotation of hands when we touch them. With the switch Phisical interaction with the snap interaction we can save the instant in which the object will be attached to the hand. (View the EasyGrasp)

