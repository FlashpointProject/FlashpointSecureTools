# Flashpoint Secure Player 2.1.7
This player attempts to solve common compatibility or portability issues posed by browser plugins on Windows for the purpose of playback in BlueMaxima's Flashpoint.

It is compatible with Windows 7, Windows 8, Windows 8.1 and Windows 10, and requires .NET Framework 4.5. If you are on Windows 8.1 or Windows 10, or if you are on Windows 7/8 and have updates enabled, you already have .NET Framework 4.5. Otherwise, you may [download .NET Framework 4.5.](http://www.microsoft.com/en-us/download/details.aspx?id=30653)

The Flashpoint Secure Player is an advanced application that makes modifications temporarily for only the duration required. It could be described as a "weak sandbox," in that it makes real changes to the computer the application is running on, but will revert these changes as soon as they are no longer needed.

Flashpoint Secure Player requires a Template and a URL. The Template determines what to do with the URL. The name of the Template to use and the URL are set via the command line.

`FlashpointSecurePlayer TemplateName URL`

This player has bugs. Help me find them! If you've found a bug, report anything unusual as an issue.

# Templates
Templates determine what to do with a URL. Every Template has its own configuration file. The configuration files may be hosted on the Flashpoint Server, making it easy to integrate into the existing Flashpoint curation flow. A number of sample configuration files are included alongside the player in the FlashpointSecurePlayerConfigs folder.

Templates consist of Modes and Modifications. Presently, there are two Modes (Web Browser Mode and Software Mode) and six Modifications (Run As Administrator, Environment Variables, Downloads Before, Registry States, Single Instance, and Old CPU Simulator.) Some Modifications may also be set via the command line.

Flashpoint Secure Player will check for a configuration file in the FlashpointSecurePlayerConfigs folder with the same name as the Template (with any invalid pathname characters replaced with a period character.) If it fails to find a configuration file in this folder, it will look for the configuration file on the Flashpoint Server at http://flashpointsecureplayerconfig/ using the same pathname rules. If the configuration file is not found in either location, an error occurs.

Here is an example of a Template that does not do anything, called "Example."

```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="flashpointSecurePlayer" type="FlashpointSecurePlayer.Shared+FlashpointSecurePlayerSection, FlashpointSecurePlayer, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null" />
  </configSections>
  <flashpointSecurePlayer>
    <templates>
      <template name="Example" />
    </templates>
  </flashpointSecurePlayer>
</configuration>
```

The important element here is the `template` element, which in future examples is all that will be shown. The rest of the configuration file will be the same in every configuration file Flashpoint Secure Player uses.

# Flashpoint Environment Variables
The Flashpoint Secure Player sets five Environment Variables for use in Templates. These Environment Variables are set immediately when the player is started so they can be used anywhere in a Template, and increase the player's flexibility.

**FP_STARTUP_PATH**

This is the path to the Flashpoint Secure Player executable.

**FP_URL**

This is the URL which was set via the command line.

**FP_ARGUMENTS**

This may either be set explicitly or implicitly. It may be set explicitly by adding `--arguments` or `-args` to the command line, and everything after will become the value of `FP_ARGUMENTS`. Otherwise, if an unrecognized argument is encountered in the command line, `FP_ARGUMENTS` will be set implicitly. Everything after and including the unrecognized argument will become the value of `FP_ARGUMENTS`. This is useful for when additional arguments need to be passed to a software, for example to pass Java Options to Java.

**FP_HTDOCS_FILE**

This is the path to the local file from the URL. The path is relative to Flashpoint's HTDOCS folder.

**FP_HTDOCS_FILE_DIR**

This is the path to the directory with the local file from the URL. The path is relative to Flashpoint's HTDOCS folder.

# Modes
The Mode determines what action Flashpoint Secure Player will perform after all of the Modifications are made. For example, the end goal may be to open a browser, or a specific software. Modes are exclusive - there may only be one set at a time.

## Web Browser Mode
The Web Browser Mode opens a URL in an Internet Explorer frame. The Flashpoint Proxy is used to go to the URL. Here is a `template` element that demonstrates the use of the Web Browser Mode.

```
<template name="webbrowserexample">
	<mode name="WEB_BROWSER" />
</template>
```

The Web Browser Mode has an additional attribute: `workingDirectory`. The `workingDirectory` attribute sets the working directory for the player. The default is the path to the Flashpoint Secure Player executable.

```
<template name="webbrowserexample">
	<mode name="WEB_BROWSER" workingDirectory="%FP_STARTUP_PATH%\Example" />
</template>
```

**Setting The Internet Explorer Version**

By default, the Internet Explorer version used in Web Browser Mode is Internet Explorer 7. Changing the Internet Explorer version to use may be accomplished by means of HTTP headers or meta elements.

Here are some examples of meta elements that may be used to change the Internet Explorer version.

 - Internet Explorer 8
 
`<meta http-equiv="X-UA-Compatible" content="IE=8" />`
 - Internet Explorer 9
 
`<meta http-equiv="X-UA-Compatible" content="IE=9" />`
 - Internet Explorer 10
 
`<meta http-equiv="X-UA-Compatible" content="IE=10" />`
 - Internet Explorer 11
 
`<meta http-equiv="X-UA-Compatible" content="IE=11" />`
 - The highest supported version of the browser (this may disable ActiveX Controls)
 
`<meta http-equiv="X-UA-Compatible" content="IE=edge" />`

The Web Browser Mode should not be used for Flash curations, as Internet Explorer is [removing Flash support](https://support.microsoft.com/en-ca/help/4520411/adobe-flash-end-of-support) December 31, 2020.

## Software Mode
The Software Mode opens any software. Here is a `template` element that demonstrates the use of the Software Mode.

```
<template name="softwareexample">
	<mode name="SOFTWARE" commandLine="Basilisk-Portable\Basilisk-Portable.exe &quot;%FP_URL%&quot;" workingDirectory="%FP_STARTUP_PATH%" hideWindow="false" />
</template>
```

The Software Mode has three additional attributes: `commandLine`, `workingDirectory` and `hideWindow`.

The `commandLine` attribute specifies the command line of the software to start. It is required for Software Mode.

The `workingDirectory` attribute sets the working directory for the process. The default is the directory with the executable of the software.

The `hideWindow` attribute causes the window of the software to be hidden. This is ideal for hiding console windows for softwares that have them. Please note that the `hideWindow` attribute is not supported when using the Old CPU Simulator Modification. For more information, see the section about the [Old CPU Simulator Modification](#old-cpu-simulator) below.

# Modifications
The Modifications determine what temporary modifications will be made for the duration of time that the application is running. Modifications are optional, none are required to be set. Modifications are not exclusive - there may be multiple Modifications set at a time.

## <a name="#run-as-administrator"></a>Run As Administrator
Set Via:
 - Command Line: `--run-as-administrator` (or `-a`)
 - Configuration File: `runAsAdministrator` attribute

When the Run As Administrator Modification is used either via command line or configuration file, if the application is not running as administrator, it will ask the user if it may launch as Administrator User. This is useful for Windows applications that require to be run as administrator.

If the Run As Administrator Modification is not used, it does not test if the application is running as administrator or not. Therefore, it is possible that, even with this Modification not active, the application could be running as administrator. The omission of this Modification simply means that no test occurs.

The command line below opens Flash as Administrator User.

`FlashpointSecurePlayer Flash "http://www.example.com/example.swf" -a`

The command line below opens the Astro Avenger II ActiveX Control as Administrator User.

`FlashpointSecurePlayer "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" "http://www.shockwave.com/content/astroavenger2/sis/index.html" -a`

The `template` element below also causes the application to be run as Administrator User.

```
<template name="example">
	<modifications runAsAdministrator="true" />
</template>
```

## Environment Variables
Set Via:
 - Configuration File: `environmentVariables` element

The Environment Variables Modification may be used to set, as well as find and replace within, environment variables for the current process and any software it launches only. The envrionment variables are not set for the entire system.

Here is a `template` element that sets the `FP_UNITY_PATH` variable to the location of the Unity Web Player plugin.

```
<template name="unitywebplayer2">
	<modifications>
		<environmentVariables>
			<environmentVariable name="FP_UNITY_PATH" value="%FP_STARTUP_PATH%\BrowserPlugins\UnityWebPlayer\Unity3d2.x\loader" />
		</environmentVariables>
	</modifications>
</template>
```

Here is a `template` element that finds and replaces within the `FP_URL` environment variable to modify the specified URL so it does not go through the Flashpoint Proxy.

```
<template name="shiva3d">
	<modifications>
		<environmentVariables>
			<environmentVariable name="FP_URL" find="http://" replace="http://localhost:22500/" />
		</environmentVariables>
	</modifications>
</template>
```

**Compatibility Layers**

It is possible to use the Environment Variables Modification to set compatibility layers by setting the `__COMPAT_LAYER` environment variable to a [compatibility fix.](http://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-7/cc765984(v=ws.10)) Here is a `template` element that starts the software in 640 x 480 resolution.

```
<template name="lowresolution">
	<modifications>
		<environmentVariables>
			<environmentVariable name="__COMPAT_LAYER" value="640x480" />
		</environmentVariables>
	</modifications>
</template>
```

## <a name="downloads-before"></a> Downloads Before
Set Via:
 - Command Line: `--download-before` (or `-dlb`)
 - Configuration File: `downloadsBefore` element

The Downloads Before Modification may be used to download files from the Flashpoint Server. This is useful for software that is only capable of loading files from the computer. The command line argument may be used more than once to download multiple files. Here is a `template` element that demonstrates the use of the Downloads Before Modification.

```
<template name="downloadsbeforeexample">
	<modifications>
		<downloadsBefore>
			<downloadBefore name="http://www.example.com/example1.swf" />
			<downloadBefore name="http://www.example.com/example2.swf" />
		</downloadsBefore>
	</modifications>
</template>
```

## <a name="registry-states"></a>Registry States
Set Via:
 - Configuration File: `registryStates` element

The Registry States Modification allows for specifying registry keys and values to be set temporarily and reverted when the player is exited. This allows for registry keys and values to only be set for the duration of time required. Environment variables may be used in values, which will be expanded.

Here is a `template` element which temporarily changes the Unity directory.

```
<template name="unitywebplayer5">
	<modifications>
		<registryStates binaryType="SCS_32BIT_BINARY">
			<registryState type="VALUE" keyName="HKEY_CURRENT_USER\SOFTWARE\UNITY\WEBPLAYER"
				valueName="DIRECTORY" value="%FP_STARTUP_PATH%\BrowserPlugins\UnityWebPlayer\Unity3d5.x"
				valueKind="String" />
		</registryStates>
	</modifications>
</template>
```

The `type` attribute of the `registryState` element specifies whether the element represents a `KEY` or `VALUE`. If not specified, the default is `KEY`.

The `keyName` and `valueName` attributes specify the location of the registry key and value.

The `valueKind` attribute specifies the kind of value that will be set.

If the `type` attribute is `KEY`, the `valueName`, `value`, and `valueKind` attributes are ignored.

There is no way to delete a registry key or value, only set them. The player may set a `_deleted` attribute, which is for internal use by the player only, and is ignored outside of the active configuration file (see the section about [Crash Recovery](#crash-recovery) below.)

Furthermore, the player may set `currentUser` and `administrator` attributes, for internal use by the player only. The Run As Administrator Modification should be used to run the application as Administrator User. For more information, see the section about the [Run As Administrator Modification](#run-as-administrator) above.

**binaryType Attribute and WOW64 Keys**

*You do not need to include the WOW6432Node or WOW64AANode subkeys when creating a Registry State.* If the `binaryType` attribute of the `registryStates` element is not set to `SCS_64BIT_BINARY`, the 32-bit registry view is used, so the WOW6432Node and WOW64AANode subkeys will be used automatically where necessary.

**ActiveX Imports**

*For curator use only.*

The ActiveX Imports feature imports a Registry State from an ActiveX Control. After the Registry State is created, the ActiveX Control may be used in curations.

As a curator using the ActiveX Imports, the ActiveX Control will be uninstalled on your machine if it was installed before. However, the benefit to this feature is that after the Registry State has been created, ActiveX Controls will only be uninstalled if they had not previously been installed on the user's machine.

To use ActiveX Imports, simply add `--activex` at the end of your command line for the game.

`FlashpointSecurePlayer "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" "http://www.shockwave.com/content/astroavenger2/sis/index.html" --activex`

Please note that you should NOT include `--activex` at the end of your command line in your final curation. The ActiveX Imports feature is only for curators to use to create a Registry State. Once the Registry State has been created using the command line above, which only needs to be done once, ever, per ActiveX Control, it will appear in the config folder with the same name, after which point it may be referenced in curations using Web Browser Mode as follows.

`FlashpointSecurePlayer "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" "http://www.shockwave.com/content/astroavenger2/sis/index.html"`

This is a command you could use in your curation, remembering to include the configuration file in the curation.

**<a name="#crash-recovery"></a>Crash Recovery**

The configuration file in the same folder as the executable is the *active configuration file.* This configuration file is maintained by the player and is not meant to be modified by curators or users (unless there is an issue with loading the file.) This file is created so that the registry may be restored in the event of a crash, shutdown or power outage.

There are four possible scenarios.

1. The software opened using Software Mode is exited using the close button.
2. The software opened using Software Mode crashes or is killed in Task Manager.
3. Flashpoint Secure Player is exited using the close button.
4. Flashpoint Secure Player crashes, is killed in Task Manager, or a shutdown or power outage occurs.

In scenarios one and two, Flashpoint Secure Player reverts the active Registry States Modification because the software is no longer open. In scenario three, Flashpoint Secure Player reverts the active Registry States Modification and any software opened using Software Mode is killed. In scenario four, Flashpoint Secure Player will revert the active Registry States Modification whenever the application is next run, regardless of what Modes or Modifications are specified.

If the Registry States Modification cannot be reverted, an error occurs and the application will exit, and not do anything else regardless of what Modes or Modifications are specified until the issue is resolved. If the registry has been modified by a different application outside of Flashpoint Secure Player, the player no longer assumes control of those registry keys and values, and the active Registry States Modification is silently discarded.

**Administrator User**

ActiveX Imports are always created as Administrator User, but it is not required during playback. Please note that in some cases, ActiveX Controls may be required to be launched as Administrator User. Flashpoint Secure Player will attempt to play the game without running as administrator by default, and in these cases, the ActiveX Control will not work unless the Run As Administrator Modification is used. For more information, see the section about the [Run As Administrator Modification](#run-as-administrator) above.

## Single Instance
Set Via:
 - Configuration File: `singleInstance` element

When the Single Instance Modification is used, the player tests that only a single instance of the software specified using Software Mode is open. This is useful for ensuring there aren't multiple instances open of software for which only a single instance may be open at a time before errors occur.

This command line in combination with this `template` element uses the Single Instance Modification when opening Basilisk.

`FlashpointSecurePlayer "basilisk" "http://www.example.com"`

```
<template name="basilisk">
	<mode name="SOFTWARE" commandLine="Basilisk-Portable\Basilisk-Portable.exe &quot;%FP_URL%&quot;" />
	<modifications>
		<singleInstance executable="basilisk.exe" strict="false" />
	</modifications>
</template>
```

The Single Instance Modification has two attributes: `executable` and `strict`.

The `executable` attribute specifies an alternate executable to test for. For example, in this instance, if the `executable` attribute were empty, it would instead look for `Basilisk-Portable.exe`.

The `strict` attribute specifies whether the test only looks for the process with the exact same pathname or any process on the machine with a matching name. The default is `false`.

## <a name="old-cpu-simulator"></a>Old CPU Simulator
Set Via:
 - Configuration File: `oldCPUSimulator` element

The Old CPU Simulator simulates running a process on a CPU with a slower clock speed in order to make old games run at the correct speed or underclock CPU intensive processes like video encoding. For more information on how to use Old CPU Simulator, [read the README.](https://github.com/tomysshadow/OldCPUSimulator)

The Old CPU Simulator Modification has six attributes. The only required attribute is `targetRate` which behaves as described in the Old CPU Simulator README. The second attribute is `refreshRate` which is optional, and behaves as described in the Old CPU Simulator README. The other attributes are `setProcessPriorityHigh`, `setSyncedProcessAffinityOne`, `syncedProcessMainThreadOnly`, and `refreshRateFloorFifteen` behave as described in the Old CPU Simulator README and default to false, true, true, and true respectively.

If the current rate is slower than the target rate, the Old CPU Simulator Modification is ignored.

The player will look for Old CPU Simulator in the OldCPUSimulator folder. It must be Old CPU Simulator 1.6.5 or newer.

# Curation Flow
Let's curate the game Zenerchi. The first step is to add the ActiveX Control to the ActiveX folder in FPSoftware. Here is the location we'll use for Zenerchi:
`ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll`

**Creating a Registry State from an ActiveX Control**

The first time an ActiveX Control is added to Flashpoint, a curator must create an ActiveX Import. This is accomplished with the following command line, substituting the ActiveX Control's location. If not run as administrator, the curator will be asked to launch as Administrator User. This command line is NOT to be included in the curation metadata, it is only used to create the Registry State.

`FlashpointSecurePlayer "ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll" "http://www.shockwave.com/content/zenerchi/sis/index.html" --activex`

This will produce a file in the FlashpointSecurePlayerConfigs folder called `activex.zenerchiweb.1.0.0.10.zenerchi.1.0.0.10.dll.config`. This file may be included in the curation as if it existed at the URL of http://flashpointsecureplayerconfigs/activex.zenerchiweb.1.0.0.10.zenerchi.1.0.0.10.dll.config and it will be downloaded from the Flashpoint Server by Flashpoint Secure Player when required.

**Using the Registry State**

To use the ActiveX Control, `--activex` is removed from the end of the command line. This is the command line the curation metadata may include.

`FlashpointSecurePlayer "ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll" "http://www.shockwave.com/content/zenerchi/sis/index.html"`

# Setup
First, the FlashpointSecurePlayerConfigs folder should be copied to the FPSoftware folder. Then, the contents of the desired build folder (Debug or Release) should be copied to the FPSoftware folder, including the x86 and amd64 subfolders.

Additionally, in order to support the Old CPU Simulator Modification, Old CPU Simulator is required. For more information, see the [Old CPU Simulator](#old-cpu-simulator) section above.

# Questions And Answers
**Is there is Linux version?**

No, but also, what? This player deals mostly in solving Windows specific problems (to do with the registry, or running as administrator, etc.) for Windows specific plugins. The types of programs Flashpoint Secure Player is useful for would not have run natively on Linux in the first place.

**Why does the Template Name need to be specified in the configuration file if the filename of the configuration file also reflects it?**

It provides additional redundancy, ensuring the configuration file loaded was the one intended, and also opens the door for potentially allowing single configuration files to have multiple `template` elements in the future.

**Shouldn't the Flashpoint Launcher just have these features built in?**

Launcher Extensions can replace the Flashpoint Secure Player to some extent, but the most advanced features are available exclusively within Flashpoint Secure Player.