# Arcweave Plugin for Unity

This plugin imports [Arcweave](https://arcweave.com/) projects into Unity. It supports data exported from **Share & export > Export > Engine > Export for Unity** (available to all users) or fetched via Arcweave's web API (Team users only).

---

## Table of Contents

- [Installation](#installation)
- [Getting Data from Arcweave](#getting-data-from-arcweave)
- [Creating an ArcweaveProjectAsset](#creating-an-arcweaveprojectasset)
- [Using the Demo Scene](#using-the-demo-scene)
- [How the Demo Works](#how-the-demo-works)
- [Plugin Documentation](#plugin-documentation)
- [Important Notes](#important-notes)
- [Troubleshooting](#troubleshooting)

---

## Installation

1. Download the plugin from the Unity Asset Store or this repository.
2. Open your Unity project.
3. Add the plugin's `/Arcweave` folder to your project's `Assets` folder.

---

## Getting Data from Arcweave

### Option 1: Export for Unity

In Arcweave, go to **Export project > Engine > Export for Unity**.

You will get a `.zip` file containing:

- `project.json`: all data of your Arcweave project.
- (Optional) `assets/Images/`: project image assets.
- (Optional) `assets/Audio/`: project audio assets.
- (Optional) `cover/cover.jpg`: the project cover image.

**File placement:**
- Place `project.json` anywhere in your Unity project's `Assets` folder.
- **For this demo project, images must go in a `Resources` folder** (create one if it doesn't exist) or they won't load at runtime.

### Option 2: Use Arcweave's Web API

Available to **Team account holders only**.

You will need:
- Your Team workspace **API key** (found in Workspace Settings).
- Your **project hash** (found in Project Properties).

See the [Arcweave API Documentation](https://docs.arcweave.com/integrations/web-api) for details.

---

## Creating an ArcweaveProjectAsset

To import your data into Unity:

1. Right-click in the Unity Assets panel.
2. Navigate to **Create > Arcweave > Project Asset**.
3. Name the new `.asset` file as you prefer.
4. Select it to open the Inspector.

### Import Settings

Choose your import source:

- **From Json**: Drag your `project.json` file into the "Project Json File" field.
- **From Web**: Enter your **API Key** and **Project Hash**.

Click the **Import Project** button to begin the import.

After successful import, the Inspector displays:
- The project name
- All global variables and their values
- An **"Open Project Viewer"** button for visual editing


---

## Using the Demo Scene

The plugin includes a `Demo` folder with a complete example scene.

### Running the Demo

1. Open **ArcweaveDemoScene** from the Demo folder.
2. Select the **ArcweavePlayer** GameObject in the hierarchy.
3. Assign your imported project asset to the `AW` field.
4. Press **Play**.

### Adding Image Assets

If your Arcweave project uses images:

1. Create a `Resources` folder in your Unity project (if it doesn't exist).
2. Copy your image files into the `Resources` folder.
3. Ensure image filenames match those in Arcweave (without extension).
4. Set each image's **Texture Type** to **Default** in Import Settings.

The plugin loads images dynamically using `Resources.Load()`.

---

## How the Demo Works

The demo consists of two main components that work together:

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      ArcweavePlayer                              │
│  (Handles narrative logic, traversal, and state management)      │
├─────────────────────────────────────────────────────────────────┤
│  Events:                                                         │
│  ├── onProjectStart    → Fired when narrative begins             │
│  ├── onElementEnter    → Fired when entering any element         │
│  ├── onElementOptions  → Fired when multiple choices available   │
│  ├── onWaitInputNext   → Fired when single path (continue)       │
│  └── onProjectFinish   → Fired when narrative ends               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ subscribes to events
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     ArcweavePlayerUI                             │
│  (Handles all visual display and user interaction)               │
├─────────────────────────────────────────────────────────────────┤
│  Responsibilities:                                               │
│  ├── Display dialogue text (RuntimeContent)                      │
│  ├── Display cover images and character portraits                │
│  ├── Create choice buttons dynamically                           │
│  ├── Handle button clicks → invoke callbacks                     │
│  └── Manage Save/Load UI                                         │
└─────────────────────────────────────────────────────────────────┘
```

### Event Flow Diagram

```
         PlayProject()
              │
              ▼
    ┌─────────────────┐
    │   Initialize    │ ← Resets all variables and visits
    │     Project     │
    └────────┬────────┘
              │
              ▼
    ┌─────────────────┐
    │ onProjectStart  │ ← UI can show "Game Started" message
    └────────┬────────┘
              │
              ▼
    ┌─────────────────┐
    │  Enter Element  │ ← element.Visits++ (incremented)
    │                 │
    └────────┬────────┘
              │
              ▼
    ┌─────────────────┐
    │ onElementEnter  │ ← UI displays content and images
    └────────┬────────┘
              │
              ▼
    ┌─────────────────┐
    │  GetOptions()   │ ← Evaluates all branch conditions
    └────────┬────────┘
              │
      ┌───────┴───────┐
      │               │
      ▼               ▼
┌───────────┐   ┌───────────┐   ┌───────────┐
│ Multiple  │   │  Single   │   │ No Paths  │
│  Paths    │   │   Path    │   │  (End)    │
└─────┬─────┘   └─────┬─────┘   └─────┬─────┘
      │               │               │
      ▼               ▼               ▼
┌───────────┐   ┌───────────┐   ┌───────────┐
│onElement- │   │onWaitInput│   │onProject- │
│  Options  │   │   Next    │   │  Finish   │
└─────┬─────┘   └─────┬─────┘   └───────────┘
      │               │
      │  callback(i)  │  callback()
      │               │
      └───────┬───────┘
              │
              ▼
    ┌─────────────────┐
    │ Execute Path    │ ← Runs Arcscript in connection labels
    │ Connection      │
    │ Labels          │
    └────────┬────────┘
              │
              ▼
         Enter Next
          Element
           (loop)
```

### Key Methods Used in Demo

| Method | Class | Purpose |
|--------|-------|---------|
| `Project.Initialize()` | Project | Reset variables and visits before starting |
| `element.Visits++` | Element | Track how many times element was visited |
| `element.GetOptions()` | Element | Get available paths from current element |
| `element.RuntimeContent` | Element | Get evaluated dialogue text |
| `element.RunContentScript()` | Element | Execute Arcscript and update RuntimeContent |
| `element.GetCoverOrFirstComponentImage()` | Element | Get element's cover or first component image |
| `path.ExecuteAppendedConnectionLabels()` | Path | Run Arcscript in connection labels |
| `path.text` | Path | Get label text or target element title |
| `options.Paths` | Options | List of valid paths to choose from |
| `options.hasPaths` | Options | Check if any paths exist |
| `options.hasOptions` | Options | Check if multiple choices available |

---

## Plugin Documentation

### Project

The root container for an imported Arcweave project. Access via `ArcweaveProjectAsset.Project`.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Project name |
| `boards` | List\<Board\> | All boards in the project |
| `components` | List\<Component\> | All components (characters/entities) |
| `Variables` | List\<Variable\> | All global variables |
| `StartingElement` | Element | Entry point for the narrative |

#### Methods

| Method | Description |
|--------|-------------|
| `void Initialize()` | **Must call before use.** Resets all variables and visit counts. |
| `Board BoardWithID(string id)` | Get board by ID |
| `Board BoardWithName(string name)` | Get board by name |
| `Element ElementWithId(string id)` | Get element by ID |
| `T GetNodeWithID<T>(string id)` | Get any node by ID and type |
| `Variable GetVariable(string name)` | Get Variable object by name |
| `bool SetVariable(string name, object value)` | Set variable value |
| `void ResetVariablesToDefaultValues()` | Reset all variables to defaults |
| `string SaveVariables()` | Serialize current variable state to JSON |
| `void LoadVariables(string json)` | Restore variable state from JSON |
| `int Visits(string elementId)` | Get visit count for an element |
| `void ResetVisits()` | Reset all visit counts to 0 |

---

### Board

Container for narrative nodes.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Name` | string | Board name |
| `Nodes` | List\<INode\> | All nodes (Elements, Branches, Jumpers) |
| `Notes` | List\<Note\> | Annotation notes |

#### Methods

| Method | Description |
|--------|-------------|
| `T NodeWithID<T>(string id)` | Get node by ID and type |
| `Element ElementWithID(string id)` | Get element by ID |

---

### Element

A narrative node containing dialogue content.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Title` | string | Element title (may contain Arcscript) |
| `RawContent` | string | Original content with Arcscript markup |
| `RuntimeContent` | string | Evaluated content (**call RunContentScript() first!**) |
| `Visits` | int | Visit counter (get/set) |
| `Components` | List\<Component\> | Attached characters/entities |
| `Attributes` | List\<Attribute\> | Custom metadata |
| `Outputs` | List\<Connection\> | Outgoing connections |
| `cover` | Cover | Cover image reference |

#### Methods

| Method | Description |
|--------|-------------|
| `void RunContentScript()` | Evaluate Arcscript in content → updates RuntimeContent |
| `Options GetOptions()` | Get available paths (evaluates branch conditions) |
| `bool HasContent()` | Check if element has content |
| `bool HasComponent(string name)` | Check if element has component by name |
| `bool TryGetComponent(string name, out Component c)` | Try to get component by name |
| `Texture2D GetCoverImage()` | Load cover image from Resources |
| `Texture2D GetFirstComponentCoverImage()` | Load first component's cover image |
| `Texture2D GetCoverOrFirstComponentImage()` | Get cover or fallback to component image |

#### Code Example

```csharp
// WRONG - RuntimeContent will be empty/outdated
string text = element.RuntimeContent;  // ❌ Don't do this!

// CORRECT - Always call RunContentScript() first
element.RunContentScript();            // ✅ Evaluates Arcscript
string text = element.RuntimeContent;  // ✅ Now it's updated

// Get available choices
Options options = element.GetOptions();
if (options.hasPaths) {
    foreach (Path path in options.Paths) {
        Debug.Log("Choice: " + path.text);
    }
}
```

---

### Connection

Link between nodes with optional label.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `RawLabel` | string | Original label with Arcscript |
| `RuntimeLabel` | string | Evaluated label (**call RunLabelScript() first!**) |
| `Source` | INode | Source node |
| `Target` | INode | Target node |
| `isValid` | bool | True if connection has valid ID |

#### Methods

| Method | Description |
|--------|-------------|
| `void RunLabelScript()` | Evaluate Arcscript → updates RuntimeLabel |

---

### Branch

Conditional branching node with if/elseif/else logic.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Conditions` | List\<Condition\> | Ordered conditions (first true wins) |
| `colorTheme` | string | Visual theme color |

---

### Condition

Single condition in a branch.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Script` | string | Arcscript condition expression |
| `Output` | Connection | Output connection if true |

#### Methods

| Method | Description |
|--------|-------------|
| `bool Evaluate()` | Evaluate condition (empty scripts return true) |

---

### Jumper

Navigation shortcut to another element.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Target` | Element | Target element to jump to |

---

### Component

Character or entity definition. **Not a Unity MonoBehaviour.**

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Name` | string | Component name |
| `Attributes` | List\<Attribute\> | Custom attributes |
| `cover` | Cover | Cover image reference |

#### Methods

| Method | Description |
|--------|-------------|
| `Texture2D GetCoverImage()` | Load cover image from Resources |

---

### Attribute

Custom metadata on elements or components.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Attribute name |
| `Type` | DataType | StringPlainText, StringRichText, or ComponentList |
| `data` | object | Attribute data (lazy-evaluated for RichText) |
| `containerType` | ContainerType | Element or Component |
| `containerId` | string | ID of the container |

---

### Variable

Global state variable. Supports: `int`, `double`, `bool`, `string`.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Variable name |
| `Value` | object | Current value |
| `DefaultValue` | object | Initial value |
| `Type` | Type | System.Type of the value |

#### Methods

| Method | Description |
|--------|-------------|
| `void ResetToDefaultValue()` | Reset to default value |

---

### Cover

Image or video reference.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `type` | Cover.Type | Image, Youtube, or Undefined |
| `filePath` | string | Asset filename |

#### Methods

| Method | Description |
|--------|-------------|
| `Texture2D ResolveImage()` | Load image from Resources folder |

---

### Options

Available paths from an element. Created by `Element.GetOptions()`.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Element` | Element | Source element |
| `Paths` | List\<Path\> | Valid paths to next elements |
| `hasPaths` | bool | True if any paths exist |
| `hasOptions` | bool | True if multiple paths OR path has label |

---

### Path

Resolved path to a target element.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `label` | string | Connection label (if any) |
| `text` | string | Label or target element's title |
| `TargetElement` | Element | Destination element |

#### Methods

| Method | Description |
|--------|-------------|
| `void ExecuteAppendedConnectionLabels()` | Run Arcscript in all connection labels along path |

---

### ArcweavePlayer

Event-driven narrative player (demo helper class).

#### Inspector Fields

| Field | Type | Description |
|-------|------|-------------|
| `aw` | ArcweaveProjectAsset | The project to play |
| `autoStart` | bool | Auto-start on scene load |

#### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `onProjectStart` | `(Project)` | Narrative started |
| `onProjectFinish` | `(Project)` | Narrative ended |
| `onElementEnter` | `(Element)` | Entered new element |
| `onElementOptions` | `(Options, Action<int>)` | Multiple choices available |
| `onWaitInputNext` | `(Action)` | Single path, waiting for continue |

#### Methods

| Method | Description |
|--------|-------------|
| `void PlayProject()` | Start or restart the narrative |
| `void Save()` | Save current state to PlayerPrefs |
| `void Load()` | Load and restore saved state |

---

## Important Notes

1. **RuntimeContent requires RunContentScript()**: The `RuntimeContent` property is not automatically evaluated. You must call `RunContentScript()` before reading it.

2. **GetOptions() has side effects**: This method internally saves and restores variable state while evaluating branch conditions.

3. **Initialize() resets everything**: Calling `Project.Initialize()` resets all variables to defaults and all visit counts to 0.

4. **Images must be in Resources folder**: Cover images are loaded via `Resources.Load()`. Place them in any `Resources` folder and ensure filenames match (without extension).

5. **Components are not MonoBehaviours**: Arcweave Components represent characters/entities in your narrative, not Unity components.

6. **Visit tracking is manual**: The `Visits` property must be incremented manually (or use ArcweavePlayer which does it automatically).

---

## Troubleshooting

### Images not loading?

**Problem:** `GetCoverImage()` returns null.

**Solution:**
- Ensure images are in a folder named exactly `Resources` (case-sensitive)
- Verify the filename matches (without extension)
- Check the image's Texture Type is set to **Default** in Import Settings

### RuntimeContent is empty or shows Arcscript markup?

**Problem:** Seeing `{variable_name}` or `if/endif` blocks in text.

**Solution:**
```csharp
// Always call this before reading RuntimeContent
element.RunContentScript();
```

### "NullReferenceException" when accessing variables?

**Problem:** Variables are null or throw errors.

**Solution:**
```csharp
// Call Initialize() once before using the project
project.Initialize();
```

### Choices/paths not appearing?

**Problem:** `GetOptions()` returns no paths or wrong paths.

**Solution:**
- Check your branch conditions in Arcweave - they might all be evaluating to false
- Verify variable values are correct: `Debug.Log(project.GetVariable("varName").Value);`
- Use the Project Viewer in Unity to visually inspect connections

### Save/Load not working?

**Problem:** `Load()` doesn't restore the correct state.

**Solution:**
- Ensure you called `Save()` before `Load()`
- PlayerPrefs persists between sessions - use `PlayerPrefs.DeleteAll()` to clear if testing
- Check the save keys exist: `PlayerPrefs.HasKey("arcweave_save_currentElement")`

### Import button is disabled?

**Problem:** Can't click "Import Project" button.

**Solution:**
- **From Json mode:** Drag a TextAsset (.json file) into the "Project Json File" field
- **From Web mode:** Fill in both API Key AND Project Hash fields
- Ensure the JSON file is valid (open it in a text editor to verify it's not corrupted)

---

## License

See the LICENSE file for details.
