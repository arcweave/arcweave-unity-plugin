# Arcweave Plugin for Unity

This plugin is for importing [Arcweave](https://arcweave.com/) projects into Unity. It uses the Arcweave data exported from **Share & export > Export > Engine > Export for Unity** (available to all Arcweave users) or fetched via Arcweave's web API (available only to Team users).


## Plugin installation

To install the Arcweave Plugin for Unity:

1. download the plugin from the Unity Assets Store or this repository.
2. open a project in Unity.
3. add the plugin's `/arcweave` subfolder to the project's `Assets` folder.

## Getting Data from Arcweave

There are 2 ways you can transfer your project's data and access them with the Arcweave Plugin for Unity:

### 1\. Export for Unity

Feature available to Pro and Team account holders. In Arcweave, go to **Share & Export > Export > Engine > Export for Unity**.

You get a .zip file containing 2 items: 

* `Json.txt`: contains all the data of your Arcweave project, minus any arcscript implementations.
* `ArcscriptImplementation.cs`: contains all arcscript implementations of your Arcweave project. (OBSOLETE: NO LONGER REQUIRED)

Place those 2 files into any subfolder within your Unity project's `Assets` folder.


### 2\. Use Arcweave's Web API

Feature available to Team account holders only. You can fetch your Arcweave project's data from within Unity, via Arcweave's web API.

To do this, you will need:

* your **API key** as an Arcweave user.
* your **project's hash**.

[This chapter](https://arcweave.com/docs/1.0/api) in the Arcweave Documentation explains where to find both of them.


## Creating an ArcweaveProjectAsset

Either way, to import your data into Unity, you must create an **ArcweaveProjectAsset** in your Unity project. To do this, right-click on your Unity Assets tab and navigate to **Create > Arcweave > Project Asset**. Name the new `.asset` file as you prefer.

Open its inspector. You will see the option to import either `From Json` or `From Web`.

* **Importing from JSON**: assign the `json.txt` file you got via "Export for Unity (see above).

* **Importing from web**: paste your **user API key** and **project hash** in the respective fields (see above).

Click **Generate Project** to begin the import and generation process. 

Once the process is completed successfully, the inspector will show the imported project name along with its global variables. This monitoring is useful for runtime debugging. 

You can also click the **"Open Project Viewer"** button to open up a window to view your imported project (all boards, elements, connection, etc.) in a visual editor.

Note: associating Arcweave projects with `ArcweaveProjectAsset` files allows you to import as many Arcweave projects as you like, within the same Unity project.


## What now?

Your project is now imported and converted to C#. 

The `ArcweaveProjectAsset` contains a `public Project project {get}` property which points to the actual project, to which the `ArcweaveProjectAsset` acts as a wrapper. 

You can use the `ArcweaveProjectAsset` according to your project's needs. See **Plugin Documentation**, below.


## Using the Demo Scene

Included in the plugin's package is also a `Demo` folder; a scene recreating Arcweave's Play Mode environment.

### Running the Demo

To see the Unity Arcweave Player demo scene in action:

 1. open the **ArcweaveDemoScene**.
 2. select the **ArcweavePlayer** game object in the hierarchy.
 3. assign your previously imported project asset into the `AW` field.
 4. hit **Play**.


### Adding Arcweave's image assets

If your Arcweave project includes image assets, the plugin can match their filenames with respective image files in any of your Unity project `Resources` folders and load them dynamically on demand.

If your project does not have a folder named `Resources`, just create one in its root folder.

The demo ArcweavePlayerUI demonstrates this dynamic image loading process.

Note: images imported in Unity for this purpose have to be set to the **Default** Texture Type, in the image import settings, for the ArcweavePlayerUI to be able to use them.


## Plugin Documentation


### Project class

You can access the Project class through the `ArcweaveProjectAsset.project` property. It is the root of an imported Arcweave project.


#### Properties

- `string name {get}`
- `List<Board> boards {get}`
- `List<Component> components {get}`
- `List<Variable> variables {get}`
- `Element startingElement {get}`

#### Methods

| Method Name | Description |
|-------------|-------------|
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


### Board class

Represents Arcweave boards.

#### Properties

 - `string id {get}`
 - `string name {get}`
 - `List<INode> nodes {get}`

#### Methods

| Method Name | Description |
|-------------|-------------|
|`T NodeWithID<T> (string id)`  |Returns the INode of type T with id.  |
|`Element ElementWithID (string id)`  |Returns the Element with id.  |


### Element class

Represents Arcweave elements.


#### Properties

 - `string id {get}`
 - `string title {get}`
 - `string rawTitle {get}`
 - `string rawContent {get}`
 - `int visits {get}`
 - `List<Component> components {get}`
 - `Cover cover {get}`
 - `List<Connection> outputs {get}`


#### Methods

| Method Name | Description |
|-------------|-------------|
|`string GetRuntimeContent ()`  |Returns the arcscript processed content.  |
|`State GetState ()`  |Returns information about possible outgoing paths taking into account conditions.  |
|`bool HasContent ()`  |Does the Element has any content at all?  |
|`bool HasComponent ()`  |Does the Element has any Component?  |
|`bool TryGetComponent (string name, out Component component)`  |Try get a Component by name.  |
|`Texture2D GetCoverImage ()`  |Returns the Texture2D cover image from a `Resources` folder.  |
|`Texture2D GetFirstComponentCoverImage ()`  |Returns the Texture2D of the first component cover from a `Resources` folder.  |


### Branch class

Represents Arcweave branches.


#### Properties

 - `string id {get}`
 - `List<Condition> conditions {get}`


#### Methods

| Method Name | Description |
|-------------|-------------|
|`Condition GetTrueCondition ()`  |Returns the true condition.  |
|`Connection GetTrueConditionOutput ()`  |Returns the Connection of the true condition. |


### Condition class

Represents Arcweave conditions (expressions used in Arcweave branches).


#### Properties

 - `string id {get}`
 - `string script {get}`
 - `Connection output {get}`


#### Methods

| Method Name | Description |
|-------------|-------------|
|`bool Evaluate ()`   |Evaluates the condition (invalid scripts return true)  |

### Component class

Represents Arcweave components.

#### Properties

 - `string id {get}`
 - `string name {get}`
 - `List<Attribute> attributes {get}`
 - `Cover cover {get}`

#### Methods

| Method Name | Description |
|-------------|-------------|
|`Texture2D GetCoverImage ()`  |Returns the Texture2D cover image from a `Resources` folder.|


### Attribute class

Represents Arcweave attributes.


#### Properties

 - `string name {get}`
 - `DataType type {get}`
 - `object data {get}`
 - `ContainerType containerType {get}`
 - `string containerId {get}`


### Connection class

Defines an Arcweave connection.

#### Properties

 - `string id {get}`
 - `string rawLabel {get}`
 - `INode source {get}`
 - `INode target {get}`

#### Methods

| Method Name | Description |
|-------------|-------------|
|`GetRuntimeLabel ()`  |Returns the arcscript processed label.|

### State class

Represents the current state of an Element with possible outgoing paths. Can be used to control the arcweave flow easier. You can create the State of an element with the `Element.GetState()` method.

#### Properties

 - `Element element {get}`: the element this state was generated from.
 - `Path[] paths {get}`: the possible paths outgoing the element.
 - `bool hasPaths {get}`: checks if there are any paths.
 - `bool hasOptions {get}`: checks if there are any options.

### Path class

Used by the State class (see above). Represents the path from a source element to a target element—if any. 

It also grants access to the valid connection's label—if any. You can use labels as option texts for the player choices.

Note: the label closest to the target element overrides the ones before it.


#### Properties

 - `string label {get}`: the valid label.
 - `Element targetElement {get}`: the target element.

### Variable class

Defines an Arcweave variable.

#### Properties

 - `string name {get}`
 - `object value {get}`
 - `Type type {get}`

#### Methods

| Method Name | Description |
|-------------|-------------|
|`void ResetToDefaultValue ()`  |Reset the variable to its default value.  |

### ArcweavePlayer

The ArcweavePlayer is provided as an example of using a project imported from Αrcweave and playing it similarly to the web app player. It is not required to utilize an arcweave imported project, but can be useful in some of your projects as-is.

#### Events

You can subscribe the events below to your game's methods.

 - `ArcweaveProjectAsset aw`
 - `bool autoStart`
 - `event OnProjectStart onProjectStart`
 - `event OnProjectFinish onProjectFinish`
 - `event OnElementEnter onElementEnter`
 - `event OnElementOptions onElementOptions`
 - `event OnWaitInputNext onWaitInputNext`

See the included demo *ArcweavePlayerUI* for examples of their use.

#### Methods

| Method Name | Description |
|-------------|-------------|
|`PlayProject ()`  |Plays the assigned arcweave project  |
|`Save ()`  |Save the current element the variables (this is done in PlayerPrefs).  |
|`Load ()`  |Loads the previously current element and the variables (from PlayerPrefs) and moves to that element  |


