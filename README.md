# Project_AI2-virtual-interaction-with-digital-phantom-for-surgical-training
---

## 📖 Project Description

This project, developed in **Unity**, implements advanced Extended Reality (XR) simulation for medical robotics. The primary objective is to simulate a cervical hernia surgery on a digital neck phantom.
Interaction is enhanced by the use of **WEART** haptic devices, which provide the user with realistic tactile and force feedback while manipulating medical instruments and interacting with tissue (e.g., muscle deformation).

## ✨ Main Features

* **WEART Integration:** Realistic manipulation of virtual objects and instruments via the WEART controller.
* **Muscle Deformation (Soft Tissue):** Realistic simulation of neck tissue deformation during interaction.
* **Clinical Workflow:** Step-by-step guided procedures for cervical hernia treatment (Cervical Workflow treatment).
* **Multithreaded Architecture:** Dedicated `Server` module for seamless communication and background data processing (e.g., `MousemotionSTDX.cs`).
* **XR Support:** Compatibility configured for Oculus headsets.

## 🧑‍⚕️ The 5-Phase Clinical Workflow

The core of this simulation is structured around a highly detailed, step-by-step clinical workflow designed to replicate a real cervical hernia intervention. The procedure is divided into 5 key phases:

1. **Phase 1: Superficial Retraction (muscle and vessels):**  Simulation starts to expose the superficial anatomical layers, in which operator push and deform the sternocleidomastoid muscle along with the carotid sheath retracting them laterally.
2. **Phase 2: Visceral Retraction:** Applying pressure with the virtual fingers, the user retracts the trachea and the laryngeal complex medially (towards the opposite side).
3. **Phase 3: Deep Dissection and Exposure:** Using the WEART controllers, it is necessary to expose the target cervical vertebrae  interacting with the prototype of ligament and retracting the deep cervical flexor muscles.
4. **Phase 4: Virtual Discectomy:** The core surgical action, requiring 
 a specific grasping or touch interaction with the target intervertebral disc. The surgeon removes the disc from the scene ecompressing
the virtual nerve roots.
1. **Phase 5: Cage Implantation:** Conclusion of the simulated surgery, the
operator grasps a 3D model of an interbody cage `Assets/Disco intervertebrale.fbx`. it navigates hrough the newly created anatomical corridor, and inserts it directly into the empty space between the two vertebrae. 
## 📁 Repository structure

The project follows the standard Unity architecture, enriched with specific modules for this medical application:
📦 Project_AI2-virtual-interaction-with-digital-phantom-for-surgical-training
 ┣ 📂 Assets/
 ┣ 📂 Cervical Workflow treatment/
 ┣ 📂 Deformation/
 ┣ 📂 Documentation WEArt/
 ┣ 📂 Recordings/
 ┣ 📂 Server/
 ┣ 📜 mesh_simplified.obj
 ┗ 📜 Progetto AI2.pptx

* `Assets/`: Contains all C# scripts, 3D models (such as `mesh_simplified.obj`), prefabs, and main scenes.
* `Cervical Workflow treatment/`: Modules and specific logic for the simulated surgical procedure phases.
* `Deformation/`: Scripts and assets for calculating muscle deformation in real time.
* `WEArt Documentation/`: Guides and technical specifications for setting up the WEART hardware.
* `Recordings/`: it considers session recordings.
* `Server/`: Client-Server  and multithreading logic to deform sternocleidomastoid muscles.
* `AI2 Project.pptx`:  presentation of the project and its 5 development phases.

## 🛠️ Requirements

To run this project correctly, you must have:

* **Unity Editor** (recommended version: 2022.3 LTS)
* **Git LFS** (Large File Storage) enabled on your Git client (essential for correctly downloading the .fbx and .obj files).
* **Oculus software** and compatible headset (if you want to test in VR).
* **WEART Middleware** (for haptic tracking and feedback).

## 🚀 Installazione e Setup

🚀 Installation and Setup

1. **Clone the repository**, making sure you have Git LFS installed:
```bash
git lfs install
git clone https://github.com/EmilioLeo/Project_AI2-virtual-interaction-with-digital-phantom-for-surgical-training.git