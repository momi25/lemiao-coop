LEMIAO CO-OP LAUNCHER 1.3 / AUTOMATIC PC CHECKS

Send Lemiao-Launcher.exe to your friend, or share the GitHub release download:
https://github.com/momi25/lemiao-coop/releases/download/lemiao-update-4/Lemiao-Launcher.zip

Both PCs need Steam, the supported Elden Ring PC version and Seamless Co-op
installed separately. Double-click the launcher. It finds the game, checks for
updates and builds a private installation from each player's own game files.
The first installation can take 10-20 minutes or longer and needs internet
access. The launcher shows which item or build stage it is preparing.
Only press PLAY SEAMLESS CO-OP when you want to start the game.

CHECKS ON EACH PC
Checks run automatically before updates, after setup and before Play. They
check Windows architecture, supported game and Seamless hashes, original game
archives, local write access, Unicode filenames, free disk space, the private
tools and final gameplay files. This build uses its own Python and .NET tools;
the Python dependency versions are pinned. System Python is not required.

CHECK THIS PC runs the full local diagnostic, including download reachability
and private runtime tests. Use EXPORT REPORT to save Lemiao-PC-check.json
and send it manually if setup fails. Reports exclude save contents, passwords,
Steam IDs and personal folder paths; nothing is uploaded automatically.
Both verified installations should show gameplay match key 82d8091b4ae8a1fb.
This key checks matching gameplay files, not live multiplayer behavior.

The complete installer was tested from scratch with newly downloaded private
tools in a folder containing spaces and non-English characters. It produced
the accepted gameplay hashes without reading saves or launching Elden Ring.
These are installation and compatibility checks; they do not prove that the
game will render, that every runtime crash is fixed, or that two-PC gameplay
works. Those still need a user-started game test on each actual PC.

If an earlier setup failed with UnicodeDecodeError while checking tasklist,
press CHECK UPDATES to download the repaired installer and retry. The installer now
reads Windows process names as bytes, regardless of the Windows language.
Launcher 1.2.1 keeps setup errors visible, requests fresh update feeds and
will not install a stale release older than its bundled repair. You do not
need to delete your saves or reinstall Elden Ring. The gameplay files are
unchanged by this setup repair; the initial build still takes several minutes.

Use CO-OP SETTINGS to set the same password on both PCs. Your own password and
other settings carry across updates. Existing rch4 saves are kept; first setup
backs up saves and copies your own character only if that profile is absent.
Compare the version displayed in the launcher before joining your friend.

CHOOSE A SAVE
Quit Elden Ring through its menu, then click CHOOSE SAVE in the launcher.
Choose your Steam save folder, click BROWSE / IMPORT, select your own save
(.sl2, .co2, .rch4 or a save backup), then click USE THIS SAVE. This selects a
whole save file, including its character slots; choose the character through
Load Game after pressing Play. Importing and selecting never start the game.

An import is copied ONCE to a separate ER0000.lXXX file in your Steam save
folder. The original and existing playthroughs are not overwritten. Future
progress is saved to that copy, and the launcher remembers your selection.
Use CHOOSE SAVE again to resume another listed profile, including Existing
save. Browsing the original again imports a NEW separate playthrough.

The chosen profile survives gameplay updates and Previous Build. Backups
are kept under %LOCALAPPDATA%\LemiaoCoop\Launcher\save-backups on import,
selection and before Play. The launcher checks the basic save container;
it cannot guarantee a save's internal integrity or reassign another person's
save to your Steam account. A missing selected file blocks Play instead of
silently importing the old original and resetting your progress.

Launcher-only updates require downloading the new launcher once. Gameplay
updates continue to install automatically from the signed mod release feed.

Whenever a new mod release is published to this repository, reopening the
launcher checks its signed update manifest and installs it automatically.
Updates wait while Elden Ring is running. They create a fresh folder, verify
the finished gameplay files, then switch the selected version. A failed build
keeps the current version and pauses retries until CHECK UPDATES is pressed.
Closing the launcher during an update cancels its build helpers, keeps the
selected version and pauses automatic retries. Press CHECK UPDATES to retry.
PREVIOUS BUILD selects the prior launcher-managed installation and pauses
automatic updates. It does not roll back saves. CHECK UPDATES resumes updates.

Installation and update checks NEVER launch Elden Ring. Opening the launcher
does not press Play. There is no background game launch or scheduled task.

The pet archive packing fault was confirmed in two crash dumps and corrected.
Other generated archives now use the native format too. Authored equipment
has remote-player _l counterparts. The installer final handoff was repaired.
Offline checks passed; this does not establish that every crash is fixed or
that pet animations and two-PC boss/pet synchronization work. Transformations
and the cosmetic pet remain experimental solo/session-host features.

Only authored source, geometry, textures, audio and the native controller are
distributed. Proprietary extracted files, saves, passwords, Seamless DLL and
the private release signing key stay on their owner's PC.

Future updates require publishing a new release; editing a local mod alone
does not update your friend's PC. The publisher tool signs each new manifest
with the private key kept in the author's workspace and increments generation.
It is not included in the friend package. Keep that key/backups private.

Launcher state and log: %LOCALAPPDATA%\LemiaoCoop\Launcher
Fresh installations: %LOCALAPPDATA%\LemiaoCoop\builds
Launcher source: Launcher.cs and Build.cs. Original code: MIT license.
