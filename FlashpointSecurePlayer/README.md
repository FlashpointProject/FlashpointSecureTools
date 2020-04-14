# Flashpoint Secure Player 1.0.8
This player attempts to solve common compatibility or portability issues posed by browser plugins on Windows for the purpose of playback in BlueMaxima's Flashpoint.

It is compatible with Windows 7, Windows 8, Windows 8.1 and Windows 10, and requires .NET Framework 4.5. If you are on Windows 8.1 or Windows 10, or if you are on Windows 7/8 and have updates enabled, you already have .NET Framework 4.5. Otherwise, you may [download .NET Framework 4.5.](http://www.microsoft.com/en-us/download/details.aspx?id=30653)

The Flashpoint Secure Player is an advanced application that makes modifications temporarily for only the duration required. It could be described as a "weak sandbox," in that it makes real changes to the computer the application is running on, but will revert these changes as soon as they are no longer needed.

It is driven by a model consisting of two concepts: Modes and Modifications. The Modes and Modifications are set either via the command line or a configuration file. The configuration files may be hosted on the Flashpoint Server, making it easy to integrate into the existing Flashpoint curation flow. A number of sample configuration files are included alongside the player in the FlashpointSecurePlayerConfigs folder.

Presently, there are three Modes (ActiveX Mode, Server Mode, and Software Mode) and six Modifications (Run As Administrator, Mode Templates, Environment Variables, Downloads Before, Registry Backups, and Single Instance.)

This player has bugs. Help me find them! If you've found a bug, report anything unusual as an issue.

# Modes
The Mode determines what action Flashpoint Secure Player will perform after all of the Modifications are made. For example, the end goal may be to open a browser, or a specific software. The Mode to use is not optional. If it is not set, an error will occur. Modes are exclusive - there may only be one set at a time.

## <a name="activex-mode"></a>ActiveX Mode
**Command Line:** `--activex` (or `-ax`)

*For curator use only.*

The ActiveX Mode imports a Registry Backup from an ActiveX Control. After the Registry Backup is created, the ActiveX Control may be used in curations.

As a curator using the ActiveX Mode, the ActiveX Control will be uninstalled on your machine if it was installed before. However, the benefit to this Mode is that after the Registry Backup has been created, ActiveX Controls will only be uninstalled if they had not previously been installed on the user's machine.

To use the ActiveX Mode, a Modification Name must be specified, which must match the path of the ActiveX Control, like so.

`FlashpointSecurePlayer --name "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" --activex`

Please note that you should NOT use the ActiveX Mode in your final curation. The ActiveX Mode is only for curators to use to create a Registry Backup. Once the Registry Backup has been created using the command line above, which only needs to be done once, ever, per ActiveX Control, it will appear in the config folder with the same name, after which point it may be referenced in curations using Server Mode as follows.

`FlashpointSecurePlayer --name "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" --server "http://www.shockwave.com/content/astroavenger2/sis/index.html"`

This is a command you could use in your curation, remembering to include the Registry Backup in the curation. For more information about Registry Backups, see the section about [Registry Backup Modifications](#registry-backups) below.

## Server Mode
**Command Line:** `--server` (or `-sv`)

The Server Mode opens a URL in an Internet Explorer frame. The Flashpoint Proxy is used to access the URL, so Server Mode is Redirectorless.

It is possible to modify the Server Mode's behaviour by using the Server Mode Template. For more information, see the section about [Mode Template Modifications](#mode-templates) below.

`FlashpointSecurePlayer --server "http://www.example.com"`

**Setting The Internet Explorer Version**

By default, the Internet Explorer version used in Server Mode is Internet Explorer 7. Changing the Internet Explorer version to use may be accomplished by means of HTTP headers or meta elements.

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

## Software Mode
**Command Line:** `--software` (or `-sw`)

The Software Mode opens any software. The command line argument *must* be the last argument specified, as everything after the command line argument will be interpreted as the command line to be passed to the software.

The command line below opens an example.swf file in Flash.

`FlashpointSecurePlayer --software "Flash\flashplayer_32_sa.exe" "http://www.example.com/example.swf"`

It is possible to modify the Software Mode's behaviour by using the Software Mode Template. For more information, see the section about [Mode Template Modifications](#mode-templates) below.

# Modifications
The Modifications determine what temporary modifications will be made for the duration of time that the application is running. Modifications are optional, none are required to be set. Modifications are not exclusive - there may be multiple Modifications set at a time.

The Modifications Name is set via the `--name` (or `-n`) command line argument. The Modifications Name is case-insensitive. When this argument is passed, Flashpoint Secure Player will check for a configuration file in the FlashpointSecurePlayerConfigs folder with the same name (with any invalid pathname characters replaced with a period character.) If it fails to find a configuration file in this folder, it will look for the configuration file on the Flashpoint Server at http://flashpointsecureplayerconfig/ using the same pathname rules. If the configuration file is not found in either location, an error occurs.

Here is an example of a Modification that does not do anything, called "Example."

```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="flashpointSecurePlayer" type="FlashpointSecurePlayer.Shared+FlashpointSecurePlayerSection, FlashpointSecurePlayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
  </configSections>

  <flashpointSecurePlayer>
    <modifications>
      <modification name="Example" />
    </modifications>
  </flashpointSecurePlayer>
  <startup>
    
  <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5"/></startup>
</configuration>
```

The important element here is the `modification` element, which in future examples is all that will be shown. The rest of the configuration file will be the same in every configuration file Flashpoint Secure Player uses.

## <a name="#run-as-administrator"></a>Run As Administrator
Set Via:
 - Command Line: `--run-as-administrator` (or `-a`)
 - Configuration File: `runAsAdministrator` attribute

When the Run As Administrator Modification is used either via command line or configuration file, if the application is not running as administrator, it will ask the user if it may launch as Administrator User. This is useful for Windows applications that require to be run as administrator.

If the Run As Administrator Modification is not used, it does not test if the application is running as administrator or not. Therefore, it is possible that, even with this Modification not active, the application could be running as administrator. The omission of this Modification simply means that no test occurs.

The command line below launches Flash as Administrator User.

`FlashpointSecurePlayer -a --software "Flash\flashplayer_32_sa.exe" "http://www.example.com/example.swf"`

The command line below launches the Astro Avenger II ActiveX Control as Administrator User.

`FlashpointSecurePlayer -a --name "ActiveX\AstroAvenger2Loader\AstroAvenger2Loader.ocx" --server "http://www.shockwave.com/content/astroavenger2/sis/index.html"`

The `modification` element below also causes the application to be run as Administrator User.

```
<modification name="example" runAsAdministrator="true" />
```
## <a name="mode-templates"></a>Mode Templates
Set Via:
 - Configuration File: `modeTemplates` element

The Mode Templates can modify the behaviour of the Server Mode and Software Mode such that a particular configuration file will cause these modes to be used. There is no ActiveX Mode Template.

**Server Mode Template**

The Server Mode Template provides the ability to apply a regex to the URL that was in the command line. For example, a `modification` element could be created which prefixes the URL with a specific domain.

```
<modification name="miniclip">
  <modeTemplates>
    <serverModeTemplate>
	  <regexes>
	    <regex name="(.+)" replace="http://www.miniclip.com/$1" />
	  </regexes>
    </serverModeTemplate>
  </modeTemplates>
</modification>
```

With the "Miniclip" Modification Name specified, all URLs passed into Server Mode will be modified, such that the command line below would cause the URL of http://www.miniclip.com/games/save-the-sheriff/en/ to open in the Internet Explorer frame.

`FlashpointSecurePlayer --name "Miniclip" --server "games/save-the-sheriff/en/"`

**Software Mode Template and hideWindow Attribute**

The Software Mode Template works identically to the Server Mode Template in that it provides the ability to replace the command line passed in with regexes. The Software Mode Template also has an additional attribute, `hideWindow`, which causes the window of the software to be hidden. This is ideal for hiding console windows for softwares that have them.

For example, a practical use of the Software Mode Template would be to create a `modification` element that always ensures the use of important Java options. Note that this example is simplified from the real Java configuration file for the purpose of demonstration.

```
<modification name="java">
  <modeTemplates>
    <softwareModeTemplate hideWindow="true">
	  <regexes>
	    <regex name="(.+)" replace="Java\JDK_1.8.0_181\bin\appletviewer.exe -J-Dhttp.proxyHost=127.0.0.1 -J-Dhttp.proxyPort=8888 -J-Dhttps.proxyHost=127.0.0.1 -J-Dhttps.proxyPort=8888 $1" />
	  </regexes>
    </softwareModeTemplate>
  </modeTemplates>
</modification>
```

With the "Java" Modification Name specified, the URL is factored into the regex, such that only the URL needs to be given when specifying the Software Mode argument. The command line below could be interpreted as passing the URL to the software.

`FlashpointSecurePlayer --name "Java" --software "http://www.example.com/example.jar"`

## Environment Variables
Set Via:
 - Configuration File: `environmentVariables` element

The Environment Variables Modification may be used to set environment variables for the current process and any software it launches only. The envrionment variables are not set for the entire system. The `%FLASHPOINTSECUREPLAYERSTARTUPPATH%` variable may be used in the value, which will be substituted with the startup path of Flashpoint Secure Player.

Here is a modification `element` that sets the FP_UNITY_PATH variable to the location of the Unity Web Player plugin.

```
<modification name="unitywebplayer2">
  <environmentVariables>
    <environmentVariable name="FP_UNITY_PATH" value="%FLASHPOINTSECUREPLAYERSTARTUPPATH%\BrowserPlugins\UnityWebPlayer\Unity3d2.x\loader" />
  </environmentVariables>
</modification>
```

**Compatibility Layers**


It is possible to use the Environment Variables Modification to set compatibility layers by setting the __COMPAT_LAYERS environment variable to a [compatibility fix.](http://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-7/cc765984(v=ws.10)) Here is a `modification` element that starts the software in 640 x 480 resolution.

```
<modification name="lowresolution">
  <environmentVariables>
    <environmentVariable name="__COMPAT_LAYERS" value="640x480" />
  </environmentVariables>
</modification>
```

## Downloads Before
Set Via:
 - Command Line: `--download-before` (or `-dlb`)
 - Configuration File: `downloadsBefore` element

The Downloads Before Modification may be used to download files from the Flashpoint Server. This is useful for software that is only capable of loading files from the computer. The command line argument may be used more than once to download multiple files. For example, this command line, in combination with a Software Mode Template, may be used to download a file for Hypercosm, which can only load files from the computer. For more information, see the section about [Mode Template Modifications](#mode-templates) above.

`FlashpointSecurePlayer --download-before "http://hypercosm/fish.hcvm" --software "http://hypercosm/fish.hcvm"`

```
<modification name="hypercosm">
  <modeTemplates>
    <softwareModeTemplate>
	  <regexes>
	    <regex name="^(\s*&quot;?)http://(.+)$" replace="$1..\..\..\Server\htdocs\$2" />
	    <regex name="(.+)" replace="Hypercosm\components\Hypercosm3D5E449320.exe $1" />
	  </regexes>
    </softwareModeTemplate>
  </modeTemplates>
</modification>
```

Here is a `modification` element that demonstrates the use of the Downloads Before Modification.

```
<modification name="downloadsbeforeexample">
  <modeTemplates>
    <downloadsBefore>
	  <downloadBefore name="http://www.example.com/example1.swf" />
	  <downloadBefore name="http://www.example.com/example2.swf" />
    </downloadsBefore>
  </modeTemplates>
</modification>
```

## <a name="registry-backups"></a>Registry Backups
Set Via:
 - Configuration File: `registryBackups` element

The Registry Backups Modification allows for specifying registry keys and values to be set temporarily and reverted when the player is exited. This allows for registry keys and values to only be set for the duration of time required. The `%FLASHPOINTSECUREPLAYERSTARTUPPATH%` variable may be used in values, which will be substituted with the startup path of Flashpoint Secure Player.

Here is a `modifications` element which temporarily changes the Unity directory.

```
<modification name="unitywebplayer2">
  <registryBackups binaryType="SCS_32BIT_BINARY">
	<registryBackup type="VALUE" keyName="HKEY_CURRENT_USER\Software\Unity\WebPlayer"
		valueName="un.Directory" value="%FLASHPOINTSECUREPLAYERSTARTUPPATH%\BrowserPlugins\UnityWebPlayer\Unity3d2.x"
		valueKind="String" />
  </registryBackups>
</modification>
```

The `type` attribute of the `registryBackup` element specifies whether the element represents a `KEY` or `VALUE`. If not specified, the default is `KEY`. The `keyName` and `valueName` attributes specify the location of the registry key and value. The `valueKind` attribute specifies the kind of value that will be set. Currently, only `String` (REG_SZ) is supported. If the `type` attribute is `KEY`, the `valueName`, `value`, and `valueKind` attributes are ignored.

There is no way to delete a registry key or value, only set them. The player may set a `_deleted` attribute, which is for internal use by the player only, and is ignored outside of the active configuration file (see the section about [Crash Recovery](#crash-recovery) below.)

**binaryType Attribute and WOW64 Keys**

*You do not need to include the WOW6432Node or WOW64AANode subkeys when creating a Registry Backup.* If the `binaryType` attribute of the `registryBackups` element is not set to `SCS_64BIT_BINARY`, the 32-bit registry view is used, so the WOW6432Node and WOW64AANode subkeys will be used automatically where necessary.

**ActiveX Imports**

See the section about [ActiveX Mode](#activex-mode) above.

**<a name="#crash-recovery"></a>Crash Recovery**

The configuration file in the same folder as the executable is the *active configuration file.* This configuration file is maintained by the player and is not meant to be modified by curators or users (unless there is an issue with loading the file.) This file is created so that the registry may be restored in the event of a crash, shutdown or power outage.

There are four possible scenarios.

1. The software opened using Software Mode is exited using the close button.
2. The software opened using Software Mode crashes or is killed in Task Manager.
3. Flashpoint Secure Player is exited using the close button.
4. Flashpoint Secure Player crashes, is killed in Task Manager, or a shutdown or power outage occurs.

In scenarios one and two, Flashpoint Secure Player reverts the active Registry Backups Modification because the software is no longer open. In scenario three, Flashpoint Secure Player reverts the active Registry Backups Modification and any software opened using Software Mode is killed. In scenario four, Flashpoint Secure Player will revert the active Registry Backups Modification whenever the application is next run, regardless of what Modes or Modifications are specified.

If the Registry Backups Modification cannot be reverted, an error occurs and the application will exit, and not do anything else regardless of what Modes or Modifications are specified until the issue is resolved. If the registry has been modified by a different application outside of Flashpoint Secure Player, the player no longer assumes control of those registry keys and values, and the active Registry Backups Modification is silently discarded.

**Administrator User**

ActiveX Imports are always created as Administrator User, but it is not required during playback. Please note that in some cases, ActiveX Controls may be required to be launched as Administrator User. Flashpoint Secure Player will attempt to play the game without running as administrator by default, and in these cases, the ActiveX Control will not work unless the Run As Administrator Modification is used. For more information, see the section about the [Run As Administrator Modification](#run-as-administrator) above.

## Single Instance
Set Via:
 - Configuration File: `singleInstance` element

When the Single Instance Modification is used, the player tests that only a single instance of the software specified using Software Mode is open. This is useful for ensuring there aren't multiple instances open of software for which only a single instance may be open at a time before errors occur.

This command line in combination with this `modification` element uses the Single Instance Modification and Mode Templates Modification to open Basilisk. For more information, see the section about [Mode Template Modifications](#mode-templates) above.

`FlashpointSecurePlayer --name "basilisk" --sw "http://www.example.com"`

```
<modification name="basilisk">
  <modeTemplates>
    <softwareModeTemplate>
	  <regexes>
	    <regex name="(.+)" replace="Basilisk-Portable\Basilisk-Portable.exe $1" />
	  </regexes>
    </softwareModeTemplate>
  </modeTemplates>
  <singleInstance strict="false" commandLine="basilisk.exe" />
</modification>
```

The Single Instance Modification has two attributes: `strict` and `commandLine`. The `strict` attribute specifies whether the test only looks for the process with the exact same pathname or any process on the machine with a matching name. The default is `false`. The `commandLine` attribute specifies an alternate command line to test for. For example, in this instance, if the `commandLine` attribute were empty, it would instead look for `Basilisk-Portable.exe`.

# Curation Flow
Let's curate the game Zenerchi. The first step is to add the ActiveX Control to the ActiveX folder in FPSoftware. Here is the location we'll use for Zenerchi:
`ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll`

**Creating a Registry Backup from an ActiveX Control**

The first time an ActiveX Control is added to Flashpoint, a curator must create an ActiveX Import. This is accomplished with the following command line, substituting the ActiveX Control's location. If not run as administrator, the curator will be asked to launch as Administrator User. This command line is NOT to be included in the curation metadata, it is only used to create the Registry Backup.

`FlashpointSecurePlayer --name "ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll" --activex`

This will produce a file in the FlashpointSecurePlayerConfigs folder called `activex.zenerchiweb.1.0.0.10.zenerchi.1.0.0.10.dll.config`. This file may be included in the curation as if it existed at the URL of http://flashpointsecureplayerconfigs/activex.zenerchiweb.1.0.0.10.zenerchi.1.0.0.10.dll.config and it will be downloaded from the Flashpoint Server by Flashpoint Secure Player when required.

**Using the Registry Backup**

To use the ActiveX Control, the same Modification Name is specified but the Mode is changed to Server Mode with the URL to load. This is the command line the curation metadata may include.

`FlashpointSecurePlayer --name "ActiveX\ZenerchiWeb.1.0.0.10\zenerchi.1.0.0.10.dll" --server "http://www.shockwave.com/content/zenerchi/sis/index.html"`

# Setup
First, the FlashpointSecurePlayerConfigs folder should be copied to the FPSoftware folder. Then, the contents of the desired build folder (Debug or Release) should be copied to the FPSoftware folder, including the x86 and amd64 subfolders. The result should be like in the figure below.

![Setup Screenshot](README_SetupScreenshot.jpg?raw=true)

You may notice that because the Flashpoint Secure Player is effectively capable of replacing many of the batch scripts in terms of functionality, the following files are no longer strictly needed. However, for the purposes of making migration easier, it may be a good idea to modify the batch scripts to call upon Flashpoint Secure Player instead of deleting them outright, reducing their complexity significantly.

- startActiveX.bat
- startActiveX_compat.bat
- startBasilisk.bat
- startBasilisk_compat.bat
- startJava.bat
- startShiVa.bat
- startUnity.bat
- unityRestoreRegistry.bat
- elevate.exe
- TWPFP
- ActiveX/unregisterAll.bat

# Planned Features
 - Currently, only the registry value kind of String (REG_SZ) is supported. Other value kinds such as Binary (REG_BINARY) and MultiStrings (REG_MULTI_SZ) will be supported in a future version.
 - Currently, there is no way to edit configuration files other than manually, and a generic "configuration file failed to load" error occurs when there is a syntax error. It would be nice to have a seperate visual editor for configuration files.
 - Generally speaking, more specific error reporting would be nice.
 - Old CPU Simulator integration?

# Questions And Answers
**Is there is Linux version?**

No, but also, what? This player deals mostly in solving Windows specific problems (to do with the registry, or running as administrator, etc.) for Windows specific plugins. The types of programs Flashpoint Secure Player is useful for would not have run natively on Linux in the first place.

**Why does the Modification Name need to be specified in the configuration file if the filename of the configuration file also reflects it?**

It provides additional redundancy, ensuring the configuration file loaded was the one intended, and also opens the door for potentially allowing single configuration files to have multiple `modification` elements in the future.

**Shouldn't the Flashpoint Launcher just have these features built in?**

No.