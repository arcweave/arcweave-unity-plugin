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

You can use the `ArcweaveProjectAsset` as best fits your project utilizing the API provided through which you can access the elements, its contents, evaluate conditions e.t.c. However included in the package is also a `Demo` folder which showcase  some aspects of the easy API provided put into use for re-creating the online Arcweave player directly within Unity alongside with an example GUI. The code and GUI contained in the `Demo` folder are also a great way to learn how to utilize your project in Unity of course.

To see the Unity Arcweave Player demo scene in action:

 1. Open the `ArcweaveDemoScene`.
 2. Select the `ArcweavePlayer` gameobject in the hierarchy and assign your previously imported project asset into the `AW` field.
 3. Hit Play.

### Utilizing Cover Images

If you utilize cover images in your Arcweave project, the plugin is also able by convention to match their files names one-to-one with image files living in any of your Unity project `Resources` folder to dynamically load them when requested via the API. The demo ArcweavePlayerUI included in the package is also made with such cover images displaying in mind.
