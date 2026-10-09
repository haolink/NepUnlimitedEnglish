# Neptunia Unlimited English patcher

This is a full toolkit allowing players to play the PS5 version of *Chou Shin
Jikuu Game Neptune: Unlimited* (*超新時空ゲイム ネプテューヌ∞ (アンリミテッド)*) in the English
language by patching the game files.

## Important preamble

**Important: This is an unofficial translation. I am not affiliated with Idea
Factory, Compile Heart, Idea Factory International or any one of the developers.
Once the actual translation is being released, please buy it and support it.**

**These texts are AI translated by Claude Code. While it was given a glossary
about Neptunia terms and databases from older games to match many wordings, any
jokes just fall flat. There are dialogs in the game about how dumb a certain
Kanji might be - and those will surely be way funnier in the actual localisation.
This translation is to shorten the wait time until the actual release which is
probably months if not more than a year away!**

**Support the devs!**

## Description

This mod might work on the Switch 1, Switch 2 or PS4 version but those might
have differing assets. Your mileage may vary. Please don't report issues about
this - but feel free to report if it works.

While it is only tested on version 1.00, it is possible to work in future
versions such as 1.04 (which is the newest one as of writing this), as I
personally never updated my PS5 past System software 13.40 to play the game I
never had access to PSN with the game to perform any update. I would imagine it
will work though. This pack includes a checksum file though - these checksums
will more than likely break in future versions.

This tool requires you to have:
- a game dump of the PS5 version (you will be given help on how to dump your own
  game here but you won't be helped searching it online) - preferably 1.00
- .NET 8.0: https://dotnet.microsoft.com/en-us/download/dotnet/8.0 - you will
  need the x64 runtime.
- toolkit has only been tested on Windows but I see no reason why it shouldn't
  work on other platforms as well - you must build it though, this pack only
  provides x64 Windows builds.
- some way to play a modified PS5 game (again some instructions)

## How to dump your own disc

- Should your PS5 be below software version 13.40, the moment you insert
  the disc you will be prompted to update. If you refuse to update to keep stuff
  such as Linux support - you can stop here. It's not possible (as of
  2026/10/06) to dump games your PS5 doesn't play back.
- If you are on 13.40, 13.42 or 13.60 you can use the [Relapse
  exploit](https://github.com/ntfargo/Relapse-Exploit) to jailbreak your
  console - the easiest explanation how to get it is to follow the
  [instructions here](https://github.com/nexgen999/ps5webkit) or to follow the
  [video instructions by MODDED
  WARFARE](https://www.youtube.com/watch?v=lWZ9B9vcVfw)
- Should your PS5 be on version 14.00 or higher - at the time of this writing,
  you will not be able to make use of this.
- Using the Relapse Exploit - open the payload manager.
- Do the following steps [as outlined in another video by MODDED
  WARFARE](https://www.youtube.com/watch?v=xIS-GrE2QPw)
  - Inside Payload manager, download ps5-app-dumper - once downloaded, launch
    it.
  - Connect a USB hard drive (formatted to exFAT) to your PS5
  - Launch the game now
  - Go into the options menu (so the disc access is minimal)
  - Access in a web browser: http://[ps5-ip]:8090
  - Make sure USB is selected as destination
  - Select "Decrypt executables"
  - Select "Enable fself"
  - Start dumping - don't quit the game. It can take a while. (35 GB of data)

Congratulations. You have your own dump now.

**I will not provide my dump to you. I will not assist you in the search online
for a disc dump. Don't do that!**

## Get the files

- Connect the USB drive to your PC
- in the folder Homebrew\PPSA22606-app0\Media\StreamingAssets\aa\PS5 you will
  find a bunch of files. These are all the Streaming Unity bundle files of the
  game.
- Copy the following ones into a folder named `input` or similar (using
  wildcards here, you need many files):
  - `database_str*.bundle`
  - `event_scenario_*.bundle`
  - `event_skit__qual_high_assets_all.bundle`
  - `interface_adv_parts__qual_high_assets_all.bundle`
  - `interface_base*.bundle`
  - `interface_cardbattle_dashboard__qual_high_assets_all.bundle`
  - `interface_dungeon_parts__qual_high_assets_all.bundle`
  - `interface_menu_parts__qual_high_assets_all.bundle`
  - `interface_worldmap_pontinfo__qual_high_assets_all.bundle`
- (note: by copying the files above, you copy 4 files more than needed but it
  doesn't matter. If you want to *REALLY* only copy the files you really need,
  feel free to look at `original-sha1.json` - if you have too much time at
  hand XD.)

## Patch them

- Download the most recent Release bundle. It'll contain:
  - `NepUnlPatcher.exe`
  - ~~`translations`~~ (folder has been removed in a force push - please get in
    touch with me if you need the files. I will provide them, they shouldn't be
    public though)
  - `original-sha1.json`
- in best case: move the `input` folder you created here
- create a folder called `output`
- and now in a command line execute:
  `.\NepUnlPatcher.exe -c original-sha1.json -t translations -i input -o output`
  (vary the command above should you have renamed stuff)
- watch the magic happen - hopefully without any error or any warning
  - should you have any while using PS5 version 1.00 - feel free to report
    what's going on - due to the fact these files cannot be shared via GitHub
    we might need to get in touch via Discord (haolink)
- you now have several bundle files in the `output` folder

## Use them

- The easiest way:
  - Copy the bundles back, overwriting the originals (they should be backed up
    in the `input` folder - feel free to make an additional backup - can't have
    too many)
  - Integrate AMPR Fix in the dump (see the [dumping video including the video
    description](https://www.youtube.com/watch?v=xIS-GrE2QPw))
    - You essentially need to copy 2 files into a sub folder `fakelib` of your
      dump but I cannot share those files here.
  - Connect the USB drive to your PS5 again.
  - In the payload manager - quit PS5 app dumper
  - Delete the game from the internal PS5 storage
  - Download and activate KStuff Lite in the Payload Manager
  - Download and activate Shadowmount Plus in the Payload Manager
  - The game on the USB drive will be detected and added to your homescreen
    - your mileage may vary, as running a PS5 game from a USB HDD can be
      painfully slow - ShadowMount Plus has a web interface on port 10101 where
      you can copy the dump back onto your internal storage of your PS5.
    - Honestly: **do that!** The game is a stutter party, even on a USB 3.0 SSD.

---
---
---
# Some information
Some of you might be interested in the general format. Don't fret. Using tools
like AssetStudioMod or AnimeStudio you will be able to read the bundle files
fine. Side note: if you plan to rip data from those - the PS5 uses a swizzled
texture format which neither the default AssetStudioMod nor AnimeStudio can
handle. Feel free to use [my custom fork of
AnimeStudio.](https://github.com/haolink/animestudio) That one can handle those
asset formats.

Inside the `Unity` folder you will see a light-weight wrapping layer of
AssetsTools.NET. This is a custom branch which I forked to add support for PS5
files which have a different alignment than PC/desktop files. Initially the
project was python based (written nearly fully by Claude Code) using UnityPy to
read and write asset bundles - but it caused the game to crash at a cutscene.
After hours of debugging it became clear the assets must never be larger and
must be aligned by 16 rather than 8 bytes - so a library was designed which
ensures proper alignment and automatic compression. Initially this was a python
library - later a C# library which was wrapped by custom code
(`UnityBundle.cs`). And then AssetsTools.NET was branched to give it PS5 support.
The branch is public and [you can check it
out](https://github.com/haolink/AssetsTools.NET/tree/ps5-support) . The branch
only slightly differs from the upstream as it only includes some alignment fix
codes.

Bundles which after saving using AssetsTools.NET turn larger than their original
counterparts will be compressed using LZ4HC. The PS5 was tested to accept those
without any issue. Should they still turn out larger despite compression the
patcher will raise an exception - in practice this should never happen - in this
project that is - should you plan to use AssetsTools.NET for major texture
replacements in the future - your mileage may vary. Other compression methods
such as LZMA were not tested.

## Building

This is a default Visual Studio 2022 (or newer) project - however you should
be able to build it using `dotnet` command line as well. It uses .NET 8.0 - make
sure to checkout all the submodules as well. If you didn't clone them, please
run `git submodule update --init` or clone immediately using
`git clone --recurse-submodules`.

The build configuration is currently only set up for Windows x64 builds -
however it has been successfully built on other platforms. My pipelines
currently only include Windows.

## Translations

The `Translation` folder within the NepUnlPatcher C# project contains
4 types of translations:

### Scenario translations

Those are the big fish. Without them - the entire project is useless. These are
the VN sequences of the game. If you open
`event_scenario_00000000__qual_high_assets_all.bundle` in AssetStudio you can
see what's happening here: most of them contain a single text asset called
`scenario_########` - relevant lines here look like this for example:

```
CINEMA_SUBTITLES	15	1390	0	000000010139001	[...]
eTALK_SET_ALL	4	100	0	000010220010003	[...]
```

I feel uncomfortable sharing actual lines of them - but the structure of the
[...] is essentially tab separated: first the name of the character speaking
7 times (probably later for: Japanese, English, Chinese (Traditional),
Chinese (Simplified), Korean, Spanish, French - before you wonder: Skits use
a CSV style format with headlines in exactly that order). Followed by Japanese
text and 6 tab sequences but no text - they will probably add all the
translations into these sequences. So the structure is like:
```
[type] [number] [offset] [digit] [voice code] [char_jp] [char_en] ... [text_jp] [text_en] ...
```

There are some more types - and some other orders. But that's the general gist.
The translator overwrites `[text_jp]` and `[char_jp]`.

### Skits

Skits are the little in-game character exchanges. Those are all stored in
`event_skit__qual_high_assets_all.bundle` - with many many TextAssets which start
with `talk` - like `talk_04030040`. Their structure is a semicolon separated
CSV style file:

```
command;image_type;image_no;face_no;emote_no;se_no;voice_no;name_jpn;name_eng;name_chi;name_chi_s;name_kor;name_spa;name_fre;msg_jpn;msg_eng;msg_chi;msg_chi_s;msg_kor;msg_spa;msg_fre;
WIPE_TALK;0;2000;1;0;0;4020030200001;c;c;c;c;c;c;c;line 1\nline 2;;;;;;;
```

Forgive me: again I don't wish to share actual game text. But you see.. that is
a really cute and simple format.

### Database

The database is... eeeeverything. Opponent names? Item entries. It's everything
and nothing. One example: `database_strdatabase__qual_high_assets_all.bundle` .

Yeah, this is pure string hell... as a MonoBehaviour file. An entry can look
like:

```json
  "datas_": [
    {
      "id_": 1001,
      "jp_text_": "JPText here",
      "en_text_": "",
      "ch_text_": "",
      "chs_text_": "",
      "kr_text_": "",
      "tag_": "START",
      "extend_": {}
    },
	...
  ]
```

### Interface labels

The smallest part but very notable. Most texts in the menus come from the database
above and get set while the game is running. Very few labels however are
hard-coded into the UI layouts themselves such as menu captions, the top menu,
development, shop and formation screens. Those live in `interface_*` bundles as
TextMeshPro components - again MonoBehaviours - and the text is stored in
their `m_text` field:

```json
{
  "m_GameObject": { "m_FileID": 0, "m_PathID": 1234567890123456789 },
  "m_Enabled": 1,
  ...
  "m_text": "JP label here",
  ...
}
```

These only make about 90 labels. But you will be confronted with them all the
time so you will notice them.

Unlike the database there is no ID here, so the patcher needs to know *where*
each label is: `translations/source/mtext.json` lists bundle file, path ID of
the MonoBehaviour and the original Japanese text. It verifies the correct
Japanese text and then replaces them using the English texts in
`translations/mtext.json` (Japanese -> English).

### Translation lists

All translations are in `json` format in the `translations` folder. They follow
a line number -> text structure (for scenarios), or id -> en structure (for
database entries), asset name/line -> en for skits and sometimes a jp -> en
text structure.

The translations have been done by Claude Code - I have not translated those.
The texts have been extracted from all relevant parts.. and then... translated -
also known as: how to burn AI tokens.

I was genuinely surprised that the game just worked after injecting several
1000 strings into it.

---
---
---

# AI disclosure

Claude Code was integral in this project. While many of the things would have
been possible by hand. It sped up development process a lot. The translations
wouldn't have been possible without it as I couldn't have translated these texts
myself, I would have had to resort to DeepL or Google Translate, the results
would have been terrible.

As of such: I will again repeat: This project does not compete with an upcoming
official translation. Please **support the devs**. This project does not compete
with custom human made translations such as the excellent [retranslation patch
for Hyperdimension Neptune: Re;Birth 2](https://steamcommunity.com/sharedfiles/filedetails/?id=542831125).
In fact: should such a human based translation appear, this project will step
aside immediately. Should they wish to use the C# code and builds to use the
injectors on a human translation: I'm all on-board, call me :P , I'll be there
in 0 seconds.

The translation JSON files have been written by tools partially by me and by
Claude Code.

The initial code in Python was heavily developed by Claude Code, the current
.NET version is designed largely by me and only AI assisted. Out of all files
`AssetValueJson.cs` is still 90% AI coded as it is a wrapper to convert between
MonoBehaviour files and JSON data - to replicate the workflow you'd use in tools
like AssetStudio where you also just see JSON. Human oversight for this file
was present at all times and also for all commits.

Other code files are designed by me and only optimised by AI for comments
(like: I initially didn't use `&lt;` in XML comments but plain `<` not thinking
about the fact, oops, this is XML) and general structure.

# Credits

- Sonic_Iso, Jordy, ntfargo, ufm42, Dr. Yenyen, TheFlow, SlidyBat, Flatz, cow,
  nhk, bollarz, Sleirsgoevy, EchoStretch and EarthOnion for the Relapse exploit.
  Their work made this possible.
- Perfare, Razmoth, Escartem for AnimeStudio/Studio
- Idea Factory and Compile Heart for continuing the Neptunia Series despite the
  shrinking fandom.
- The Neptunia community for being supportive
- My family and my friends for being supportive.
- and you!