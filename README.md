# Arcweave Unity Plugin

Arcweave Unity Plugin is a plugin for importing Arcweave Projects from  [arcweave.com](https://arcweave.com/)  in Unity and using them in your Projects.

The Arcweave Unity Exports currently are offered to our Pro and Team Accounts.

The export consist from two files, one being the `Json.txt` and the other being the `ArcscriptImplementation.cs`. The `Json.txt` file contains all the data structure of an Arcweave project, less any arcscript implementations which are part of the already pre-generated `ArcsriptImplmentations.cs` file. Aside from exporting the project from the Arcweave app and importing it in Unity with this plugin, the plugin is also able to fetch (and generate accordingly) these files via the Web API directly.

## Installing the Plugin

Download the plugin and add the  `/arcweave`  folder in your project's  `assets`  folder. 

## Importing your project

You can import your project in two ways, either by using our Web API or by downloading the Unity Export `.zip` and extracting its contents (`json.txt` and `ArcscriptImplementations.cs`) into any folder within your Unity project `assets` folder.

In both cases, you will need to create an `ArcweaveProjectAsset` in your Unity project. To do this, right-click in your Unity Assets tab and navigate to `Create/Arcweave/Project Asset`. A new `.asset` file will be created. Within its inspector you can now choose to either import `From Json`, or import `From Web`.

 - If you choose `From Json`, you need to assign the `json.txt` that was part of the Unity Export from the Arcweave app.
 - If you choose `From Web`, you need to specify your `User API Key` as well as the target `Project Hash` in the respective fields.

Click `Generate Project` to begin the import and generation process. Once the process is completed succesfully, the inspector will show the imported project name along with its global variables (useful for debugging in runtime as well). If you chose `From Web`, a C# file called `ArcscriptImplementations.cs` will also be generated automatically alongside the `ArcweaveProjectAsset` file.

Your project is now imported and converted to C#. The `ArcweaveProjectAsset` contains a `public Project project {get}` property which points to the actual project, while the `ArcweaveProjectAsset` acts as a wrapper to that. You can have as many Arcweave projects imported this way within the same Unity project as you like.

## Using The Imported Project

You can use the `ArcweaveProjectAsset` as best fits your project utilizing the API provided through which you can access the elements, its contents, evaluate conditions, traverse possible paths e.t.c. However included in the package is also a `Demo` folder which showcase  some aspects of the easy API provided put into use for re-creating the online Arcweave player directly within Unity alongside with an example GUI. The code and GUI contained in the `Demo` folder are also a great way to learn how to utilize your project in Unity of course.

To see the Unity Arcweave Player demo scene in action:

 1. Open the `ArcweaveDemoScene`.
 2. Select the `ArcweavePlayer` gameobject in the hierarchy and assign your previously imported project asset into the `AW` field.
 3. Hit Play.

### Utilizing Cover Images

If you utilize cover images in your Arcweave project, the plugin is also able by convention to match their files names one-to-one with image files living in any of your Unity project `Resources` folder to dynamically load them when requested via the API. The demo ArcweavePlayerUI included in the package is also made with such cover images displaying in mind. Do note that the images imported in Unity for this purpose have to be set to the "Default" Texture Type in the image import settings (if not already) for the Demo ArcweavePlayerUI to be able to use them.

## API Documentation

### Project
You can access the Project class through the `ArcweaveProjectAsset.project` property. It is the root of an imported Arcweave project.

**Properties**

- `string name {get}`
- `List<Board> boards {get}`
- `List<Component> components {get}`
- `List<Variable> variables {get}`
- `Element startingElement {get}`

**Methods**
|Method Name|Description  |
|--|--|
|`void Initialize ()`  |Should be called once before using the project.  |
|`Board BoardWithID (string id)`  |Returns the Board with id.  |
|`Board BoardWithName (string name)`  |Returns the Board with name.  |
|`T GetNodeWithID<T> (string id)`  |Returns the INode of type T with id.   |
|`T GetVariable<T> (string name)`  |Returns the variable value of type T with name.  |
|`object GetVariable (string name)`  |Returns the variable object value with name.  |
|`bool SetVariable (string name, object value)`  |Sets the variable with name to a new value. |
|`void ResetVariablesToDefaultValues ()`  |Reset all variables to their default value.  |
|`string SaveVariables ()`  |Returns a string of the saved variables that can be loaded later.  |
|`void LoadVariables (string save)`  |Loads a previously saved string made with SaveVariables.  |

### Board
Defines an Arcweave board.

**Properties**

 - `string id {get}`
 - `string name {get}`
 - `List<INode> nodes {get}`

**Methods**
|Method Name  |Description  |
|--|--|
|`T NodeWithID<T> (string id)`  |Returns the INode of type T with id.  |
|`Element ElementWithID (string id)`  |Returns the Element with id.  |


### Element
Defines an Arcweave element.

**Properties**

 - `string id {get}`
 - `string title {get}`
 - `string rawContent {get}`
 - `List<Component> components {get}`
 - `Cover cover {get}`
 - `List<Connection> outputs {get}`

**Methods**

|Method Name|Description  |
|--|--|
|`string GetRuntimeContent ()`  |Returns the arcscript processed content.  |
|`State GetState ()`  |Returns information about possible outgoing paths taking into account conditions.  |
|`bool HasContent ()`  |Does the Element has any content at all?  |
|`bool HasComponent ()`  |Does the Element has any Component?  |
|`bool TryGetComponent (string name, out Component component)`  |Try get a Component by name.  |
|`Texture2D GetCoverImage ()`  |Returns the Texture2D cover image from a `Resources` folder.  |
|`Texture2D GetFirstComponentCoverImage ()`  |Returns the Texture2D of the first component cover from a `Resources` folder.  |

### Branch
Defines an Arcweave branch.

**Properties**

 - `string id {get}`
 - `List<Condition> conditions {get}`

**Methods**
|Method Name  |Description  |
|--|--|
|`Condition GetTrueCondition ()`  |Returns the true condition.  |
|`Connection GetTrueConditionOutput ()`  |Returns the Connection of the true condition. |

### Condition
Defines an Arcweave condition that lives within a branch.

**Properties**

 - `string id {get}`
 - `string script {get}`
 - `Connection output {get}`

**Methods**
|Method Name  |Description  |
|--|--|
|`bool Evaluate ()`   |Evaluates the condition (invalid scripts return true)  |

### Component
Defines an Arcweave component.

**Properties**

 - `string id {get}`
 - `string name {get}`
 - `List<Attribute> attributes {get}`
 - `Cover cover {get}`

**Methods**
|Method Name  |Description  |
|--|--|
|`Texture2D GetCoverImage ()`  |Returns the Texture2D cover image from a `Resources` folder.|

### Component.Attribute
Defines the attribute of a component.

**Properties**

 - `string name {get}`
 - `DataType type {get`
 - `object data {get}`

### Connection
Defines an Arcweave connection.

**Properties**

 - `string id {get}`
 - `string label {get}`
 - `INode source {get}`
 - `INode target {get}`

### State
Represents the currenst state of an Element with possible outgoing paths. Can be used to control the arcweave flow easier. You can create the State of an element with the `Element.GetState()` method.

**Properties**

 - `Element element {get} // The element this state was generated from.`
 - `Path[] paths {get} // The possible paths outgoing the element.`
 - `bool hasPaths {get} // Utility check if there are any paths.`
 - `bool hasOptions {get} // Utility check if there are actually any options.`

### Path
Represents the path from an Element to the next possible Element if any, with the according label that led to that Element. The State class above make use of Paths.

**Properties**

 - `string label {get} // The last label that lead to the target element.`
 - `Element targetElement {get} // The element that this path will lead/lad to.`

### Variable
Defines an Arcweave variable.

**Properties**

 - `string name {get}`
 - `object value {get}`
 - `Type type {get}`

**Methods**
|Method Name  |Description  |
|--|--|
|`void ResetToDefaultValue ()`  |Reset the variable to its default value.  |

### ArcweavePlayer
The ArcweavePlayer is provided as an example of using an arcweave imported project and playing it similarily to the web app player. It is not required to utilize an arcweave imported project, but can be usefull in some of your projects as-is.

**Properties**

 - `ArcweaveProjectAsset aw`
 - `bool autoStart`
 - `event OnProjectStart onProjectStart`
 - `event OnProjectFinish onProjectFinish`
 - `event OnElementEnter onElementEnter`
 - `event OnElementOptions onElementOptions`
 - `event OnWaitInputNext onWaitInputNext`

The events above can be subscribed to. The demo ArcweavePlayerUI included makes use of these events for example.

**Methods**

|Method Name  |Description  |
|--|--|
|`PlayProject ()`  |Plays the assigned arcweave project  |
|`Save ()`  |Save the current element the variables (this is doe in PlayerPrefs).  |
|`Load ()`  |Loads the previously current element and the variables (from PlayerPrefs) and moves to that element  |

