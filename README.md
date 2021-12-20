# View Synchronization Utility
Just a small little tool to automate copying over view files when developing

You specify a solution directory and a wwwroot directory, then the tool will listen to changes being done to *.cshtml files and sync them accordingly.
Supports the following events:
* Created
* Modified
* Removed
* Renamed

Misc features
* It will clean up the folder in the wwwroot if a remove event results in an empty folder.
* Can be controlled through a trayicon.
* Can send notification each time an action is taken (this can be spammy, disabled by default).
* Can log every action it takes to file (enabled by default).



The code isn't very pretty, and it depends on a few specific DLL's to be installed when building.
Will (maybe) fix later.
