# 3D replay (Unity)

`ERSim/` is a Unity **6000.3.12f1** (Unity 6.3 LTS) project that replays an event log
(`docs/event_log_format.md`) in 3D. It only plays the CSV back: the clock, queue lengths,
nurse state and busy rooms on screen are the `time`, `triage_queue`, `room_queue`,
`nurse_busy` and `rooms_busy` columns of the last replayed row. Nothing is calculated in Unity.

## Open and run

1. Clone with [Git LFS](https://git-lfs.com) installed (`git lfs install`); the models and
   their texture are LFS files.
2. Unity Hub > Add > Add project from disk > `unity/ERSim`.
3. Open `Assets/Scenes/Main.unity` and press Play.

The scene file is empty. Everything is created from code when the scene loads
(`Assets/Scripts/Bootstrap.cs`): the building, furniture, staff, camera, lights and UI.

## Event logs

`data/events_rooms{R}_ia{IA}.csv` is the single source. The editor copies those files into
`Assets/StreamingAssets` when it opens and before every build (`Assets/Editor/DataSync.cs`,
or the menu **ERSim > Sync event logs from data**). The copies are not committed.

## Controls

| Control | Action |
| --- | --- |
| Play / Pause, Restart | Playback |
| 1x / 10x / 60x | Speed; 1x is one simulated minute per second |
| Scenario buttons | Base, A, B, C, D from `docs/assumptions.md`; rooms 4 / 6 / 8 are shown accordingly |
| Drag | Orbit the camera |
| Mouse wheel | Zoom |

Patients are coloured by severity through the disc under their feet and the marker above
their head: green = low, yellow = medium, red = high. Staff have a blue disc.
Walls between the camera and the inside of the building are hidden automatically.

## Build

Menu **ERSim > Build Windows**, or from a terminal:

```
Unity.exe -batchmode -quit -projectPath unity/ERSim -executeMethod ERSim.EditorTools.BuildScript.BuildWindows
```

The player is written to `unity/ERSim/Builds/Windows/ERSim.exe` (ignored by git).

Screenshots for the report can be taken from the command line; each entry is
`file,sim time,scenario index,yaw,pitch,distance,target x,target z`:

```
ERSim.exe -shots "overview.png,300,0,0,48,50,-1.5,-1|room.png,300,0,25,35,11,13.6,4"
```

## Code

| File | Role |
| --- | --- |
| `Scripts/EventLog.cs` | Reads the CSV |
| `Scripts/ReplayController.cs` | Applies rows as the clock passes their time; holds the queues' order |
| `Scripts/Patient.cs` | A character that walks along waypoints, idles and sits on the bed |
| `Scripts/ErLayout.cs` | Building, rooms, queue positions (empty objects), routes |
| `Scripts/Models.cs` | Loads and places the models; wall cutaway |
| `Scripts/ReplayUI.cs`, `Scripts/OrbitCamera.cs` | On-screen panel and camera |
| `Editor/ProjectSetup.cs` | Visible Meta Files, Force Text, main scene, build settings |
| `Editor/ModelImportSettings.cs` | Import settings of the models |

## Third-party assets

Models in `ERSim/Assets/Resources/Models` are from [Kenney](https://kenney.nl)
(Mini Characters, Furniture Kit), licensed CC0. The licence files are next to the models.
