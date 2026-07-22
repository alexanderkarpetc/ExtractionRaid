using UnityEngine;

namespace View.Audio
{
    public sealed class GameAudioClipLibrary
    {
        public readonly AudioClip[] PistolClose = Load("Audio/Weapons/Pistol/Close");
        public readonly AudioClip[] PistolDistant = Load("Audio/Weapons/Pistol/Distant");
        public readonly AudioClip[] PistolDryFire = Load("Audio/Weapons/Pistol/DryFire");
        public readonly AudioClip[] PistolReload = Load("Audio/Weapons/Pistol/Reload");
        public readonly AudioClip[] PistolHolster = Load("Audio/Weapons/Pistol/Holster");
        public readonly AudioClip[] PistolUnholster = Load("Audio/Weapons/Pistol/Unholster");
        public readonly AudioClip[] RifleFire = Load("Audio/Weapons/Rifle/Fire");
        public readonly AudioClip[] RifleDryFire = Load("Audio/Weapons/Rifle/DryFire");
        public readonly AudioClip[] RifleReload = Load("Audio/Weapons/Rifle/Reload");
        public readonly AudioClip[] ShotgunFire = Load("Audio/Weapons/Shotgun/Fire");

        public readonly AudioClip[] HardSurfaceImpacts = Load("Audio/Impacts/HardSurface");
        public readonly AudioClip[] MetalImpacts = Load("Audio/Impacts/Metal");
        public readonly AudioClip[] FleshImpacts = Load("Audio/Impacts/Flesh");
        public readonly AudioClip[] ArmorImpacts = Load("Audio/Impacts/Armor");
        public readonly AudioClip[] Ricochets = Load("Audio/Impacts/Ricochet");
        public readonly AudioClip[] Headshots = Load("Audio/Impacts/Headshot");
        public readonly AudioClip[] Bleeding = Load("Audio/Status/Bleeding");
        public readonly AudioClip[] BodyFalls = Load("Audio/Characters/BodyFall");
        public readonly AudioClip[] Footsteps = Load("Audio/Movement/Footsteps");
        public readonly AudioClip[] BackpackOpen = Load("Audio/UI/Backpack");
        public readonly AudioClip[] HideoutMusic = Load("Audio/Music/Hideout");

        public readonly AudioClip[] PmcAreaSecure = LoadNamed("Audio/Voices/PMC", "PMC_Area_Secure_01");
        public readonly AudioClip[] PmcContact = LoadNamed("Audio/Voices/PMC", "PMC_Contact_01");
        public readonly AudioClip[] PmcCoverMe = LoadNamed("Audio/Voices/PMC", "PMC_Cover_me_01");
        public readonly AudioClip[] PmcGrenade = LoadNamed("Audio/Voices/PMC", "PMC_Grenade_01");
        public readonly AudioClip[] PmcHeardSomething = LoadNamed("Audio/Voices/PMC", "PMC_hold_up_01");
        public readonly AudioClip[] PmcHit = LoadNamed("Audio/Voices/PMC", "PMC_Iam_Hit_01");
        public readonly AudioClip[] PmcLostVisual = LoadNamed("Audio/Voices/PMC", "PMC_lost_visual_01");
        public readonly AudioClip[] PmcManDown = LoadNamed("Audio/Voices/PMC", "PMC_Man_Down_01");
        public readonly AudioClip[] PmcMoving = LoadNamed("Audio/Voices/PMC", "PMC_Moving_01");
        public readonly AudioClip[] PmcReloading = LoadNamed("Audio/Voices/PMC", "PMC_Reloading_01");
        public readonly AudioClip[] PmcStaySharp = LoadNamed("Audio/Voices/PMC", "PMC_stay_sharp_01");

        public readonly AudioClip[] ScavCheckPockets = LoadNamed("Audio/Voices/SCAV", "SCAV_check_pockets_01");
        public readonly AudioClip[] ScavBastard = LoadNamed("Audio/Voices/SCAV", "SCAV_bastard_01");
        public readonly AudioClip[] ScavHelp = LoadNamed("Audio/Voices/SCAV", "SCAV_help_01");
        public readonly AudioClip[] ScavHit = LoadNamed("Audio/Voices/SCAV", "SCAV_ugh_01");
        public readonly AudioClip[] ScavHeadshot = LoadNamed("Audio/Voices/SCAV", "SCAV_oh_shit_01");
        public readonly AudioClip[] ScavReloading = LoadNamed("Audio/Voices/SCAV", "SCAV_empty_01");
        public readonly AudioClip[] ScavContact = LoadNamed("Audio/Voices/SCAV", "SCAV_shoot_him_01");
        public readonly AudioClip[] ScavHeardSomething = LoadNamed("Audio/Voices/SCAV",
            "SCAV_what_01", "SCAV_what_02");

        static AudioClip[] Load(string path) => Resources.LoadAll<AudioClip>(path);

        static AudioClip[] LoadNamed(string path, params string[] names)
        {
            var clips = new AudioClip[names.Length];
            for (int i = 0; i < names.Length; i++)
                clips[i] = Resources.Load<AudioClip>($"{path}/{names[i]}");
            return clips;
        }
    }
}
