# FancyTextExtended

This mod for Celeste implements some extra functionality for Dialog entries that are used with Fancy Text, such as Textbox messages.

When using features from this mod, please ensure that you include it in your `everest.yaml`. Also be sure to update the referenced version when using newer features.

```yaml
  Dependencies:
    - Name: FancyTextExtended
      Version: 0.1.0
```

## Dialog commands

### **`{ftx:notalk}` ... `{/ftx:notalk}`**
This makes it so that when these words appear, the character stops talking and their portrait animation goes to idle.

### **`{ftx:input ...}`**
This inserts an input graphic into the text. What graphic is inserted depends on the arguments passed. The possibilities are as follows:

- **`<input_name>`**: A registered name of the input is passed as the first argument.

  There are three types of input used throughout the game: **Button**, **Integer Axis**, and **Joystick**. Which binding is used for the graphic depends on the input type and any additional arguments that may be required.

  - **Button**: Represented as a boolean. Has one binding. No additional arguments are required.
    
    *Example: `{ftx:input MenuConfirm}` displays the binding mapped to Confirm.*

  - **Integer Axis**: Represented as an integer that is either -1, 0, or 1. Has two main bindings - one for the negative value, the other for the positive value - as well as an alternative binding for each. Requires an additional argument:

    - **`<direction(int)>`**: Accepts a non-zero negative or positive number (`-1` or `1`).

    *Example: `{ftx:input MoveY -1}` displays the binding mapped to gameplay Up, and `{ftx:input MoveY 1}` displays the binding mapped to gameplay Down.*

  - **Joystick**: Represented as a vector with both X and Y values between -1 and 1. Has four main bindings - each for up, down, left, and right respectively - as well as an alternative binding for each. Requires an additional argument:

    - **`<direction>`**: Accepts one of the following (case-insensitive): `Up`, `Down`, `Left`, or `Right`.

    *Example: `{ftx:input Aim Right}` displays the corresponding binding, which is the one mapped to gameplay Right.*

  An input graphic inserted this way will be automatically updated when the binding changes, or when a controller is plugged in. If needed, this will also recalculate the positions of its following characters as well as the width of the line.

  *Example: if Jump is bound to the C key, and the keyboard is in focus, the graphic inserted by `{ftx:input Jump}` displays the C key.*

  <details>
    <summary>List of registered input names and their input type: (click to expand)</summary>

      "ESC" - Button:
        The Escape key. Usually the alternative to Pause or MenuCancel.
      
      "Pause" - Button:
        Pauses the gameplay and brings up the pause menu.
      
      "MenuLeft" - Button:
        Used in menus to navigate to the left.
      
      "MenuRight" - Button:
        Used in menus to navigate to the right.
      
      "MenuUp" - Button:
        Used in menus to navigate upwards.
      
      "MenuDown" - Button:
        Used in menus to navigate downwards.
      
      "MenuConfirm" - Button:
        Used in menus to confirm or advance.
      
      "MenuCancel" - Button:
        Used in menus to cancel or go back.
      
      "MenuJournal" - Button:
        Used in the overworld to bring up the journal.
      
      "QuickRestart" - Button:
        Pauses the gameplay and goes straight to the restart menu.
      
      "MoveX" - Integer Axis:
        Moves the player left (-ve) or right (+ve).
      
      "MoveY" - Integer Axis:
        Makes the player look up (-ve) or crouch down (+ve) when grounded, or moves them up or down when holding on to a wall. If in mid-air, bringing it down makes the player fall faster.
      
      "GliderMoveY" - Integer Axis:
        Used when the player is holding a Glider to either slow their descent (up, -ve) or make them fall faster (down, +ve). Shares bindings with MoveY.
      
      "Aim" - Joystick:
        Used by the player to aim their dash or boost, and also to move the view when using a Lookout. Shares bindings with MoveX and MoveY.
      
      "Feather" - Joystick:
        Used by the player in swim state or Feather state to navigate. Shares bindings with MoveX and MoveY.
      
      "MountainAim" - Joystick:
        Used in the overworld and level complete screens to pan the view. Hard bound to keys WASD and the right thumbstick.
      
      "Jump" - Button:
        Makes the player jump from the ground. Releasing it cuts their jump height short.
      
      "Dash" - Button:
        Makes the player start dashing.
      
      "Grab" - Button:
        When held down, makes the player grab and hold on to a wall when close to it, or pick up a holdable object.
      
      "Talk" - Button:
        Makes the player interact with NPCs and various objects.
      
      "CrouchDash" - Button:
        A relatively recent addition as a binding, which makes the player execute an advanced move where they enter a crouched state mid-air and start dashing. That crouched state is maintained throughout the dash, allowing the player to squeeze through tight gaps.
        More commonly called the "Demodash" in honour of Demo Jameson who discovered the move.
  </details>

  This mod also provides an export for registering inputs from other mods. See the ModInterop section further below.

- **`<x(int)> <y(int)>`**: This indicates a direction input graphic. Both arguments represent the X and Y values of the direction, and accept a negative number, zero, or a positive number (`-1`, `0`, or `1`).
  
  *Example: `{ftx:input 1 0}` inserts an arrow pointing to the right.*

## Portraits Sprite Bank attributes

This mod also provides some extra attributes for your portraits sprite elements.

### **`ftx.phonestaticEvent` (string)**

Plays a background static noise when a portrait with this attribute is active. Accepts an audio event name as the value.

> [!Note]
> 
> While the `phonestatic` attribute already exists in the base game, it is unfortunately hard-coded for two values - `"ex"` and `"mom"`. That can be used if you want to simply use one of the two vanilla events. Otherwise, you'll have to use this modded attribute.

### **`ftx.easeSfx` (string)**

Plays a custom sound when the textbox from this portrait ID opens or closes. Accepts a value which refers to two similarly named audio events - one suffixed with `_in`, and the other suffixed with `_out`.

By default, the base game plays the `event:/ui/game/textbox_madeline` events when the active portrait's sprite name contains `madeline`, and the `event:/ui/game/textbox_other` events otherwise.

## ModInterop

So far, this mod provides one exported function for registering inputs from other mods for use with the `{ftx:input ...}` Dialog command.

This function called **`RegisterInputName`** has two parameters:

- **`string name`**: The name of the input. It's recommended to include a reference to your mod in order to avoid potential conflicts.

- **`Func<VirtualInput> inputGetter`**: A callback that gets the input field as a `VirtualInput` directly from the mod that contains it.

In order to use it, you import this class into your mod project:
```cs
[ModImportName("FancyTextExtended")]
public static class FancyTextExtImports
{
    public static Action<string, Func<VirtualInput>> RegisterInputName;
}
```

Then you include these lines into your project's module:
```cs
// top of class file
using MonoMod.Utils;

// body of Load() method
typeof(FancyTextExtImports).ModInterop();
```

See [this Everest wiki article](https://github.com/EverestAPI/Resources/wiki/Cross-Mod-Functionality#modinterop) to know more about ModInterop.
