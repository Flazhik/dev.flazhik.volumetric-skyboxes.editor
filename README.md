# Volumetric Skyboxes Editor

## Wiki
[Visit a wiki](https://github.com/Flazhik/dev.flazhik.volumetric-skyboxes.editor/wiki) for some extra info on the editor.

## Installation

If you already have a Rude Editor Unity project deployed somewhere, I would recommend just using it as a backbone for your project: custom maps and skyboxes won't conflict and you'd have ULTRAKILL assets at your disposal

If you're still unwilling to do this, just create an empty Unity 3D project with Unity version **2022.3**

Open a Package Manager (**Window** -> **Package Manager**), press **+** -> **Add package from git URL**.

Enter `git+https://github.com/Flazhik/dev.flazhik.volumetric-skyboxes.editor` and press **Add**

## Usage
Open the editor menu located at **Skyboxes** -> **Skyboxes menu**

First, you'll need to set a path to the folder where your skyboxes are gonna be exported. Click the button with a gear icon to set it. `{path_to_a_common_steam_folder}/ULTRAKILL/Cybergrind/VolumetricSkyboxes` is a preferable option because that's the folder the mod expects skyboxes to be in. Create `VolumetricSkyboxes` folder manually if it doesn't exist yet.

Everything else should be relatively straightforward from here.

## Important stuff to know before you begin

### Don't touch ArenaAnchor GameObject
It defines relative position of a skybox to the arena. Skyboxes without this object will fail to load.

### Built-in Unity shaders won't work

If you're using an empty, non-Rude project as your editor environment, materials with Built-in Unity shaders won't render in game, leaving you with a pink mess instead.
You're gonna have to either include shaders in your skybox assets (for instance, these shaders can be found [here](https://github.com/TwoTailsGames/Unity-Built-in-Shaders/tree/master/DefaultResourcesExtra)) or use some other 3rd party shader.

The preferable option, of course, is to use ULTRAKILL shaders instead as it would fit the game stylistics better. Yeah, as you can see I'm not biased about if you should use Rude at all.

### Custom MonoBehaviours are not supported
You can still use components that are part of ULTRAKILL assembly if you're using this editor in a Rude project, but custom assemblies won't be supported due to security concerns.

### Some components are gonna be disabled in-game
Colliders, every component derived from `MonoSingleton`, and some other components are gonna be removed in-game. Most of them are still allowed though.

### Light sources in a skybox only affect skybox itself
And vice versa: arena lighting doesn't affect skyboxes.

### Skyboxes are going to be placed on special layers in game

It doesn't matter which layers your GameObjects are on, all of them are gonna be assigned to `SandboxGrabbable` (layer 29) layer in ULTRAKILL with an exception of objects on `SpecialLighting` (layer 31) layer. 
As for the lights, they're gonna act like this:

1. If a culling mask of a light source includes `SpecialLighting` (layer 31) layer _and_ excludes `SandboxGrabbable` (layer 29), it's gonna illuminate `SpecialLighting` layer of a skybox
2. It's only gonna illuminate `SandboxGrabbable` layer otherwise

> **Always keep it in mind while managing your game obejcts and light sources**

Now, I understand all this raises a bunch of logical question and I'll try to answer at least some of them.

**Q**: Why `SandboxGrabbable`? Why `SpecialLighting`?

**A**: No reason in particular, I just needed to pick a layer that is mostly unused in Cybergrind. As for the `SpecialLighting` layer, it's allocated to let people have at least some degree of freedom and two layers could be illuminated separately.

**Q**: Why not just let objects stay where they are?

**A**: Some layers in ULTRAKILL make GameObjects behave differently. But the most important reason is the way the mod works. It renders all the content of every skybox behind everything else at all times, plus it needs to keep skybox / arena illumination apart.
Both these tasks are acomplished by using culling masks, which forces me to keep GameObject of a skybox on a dedicated layers.

**Q**: Then why didn't you just use a shader to achieve this instead?

**A**: Future versions of the mod might do just that.

### Static meshes are not supported
If a mesh in your skybox is a part of a combined static mesh it's gonna be removed in-game.

So don't get surprised if some of your objects suddenly dissapear in ULTRAKILL. That's most probably the reason why.