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

        public readonly AudioClip[] HardSurfaceImpacts = Load("Audio/Impacts/HardSurface");
        public readonly AudioClip[] MetalImpacts = Load("Audio/Impacts/Metal");
        public readonly AudioClip[] FleshImpacts = Load("Audio/Impacts/Flesh");
        public readonly AudioClip[] ArmorImpacts = Load("Audio/Impacts/Armor");
        public readonly AudioClip[] Ricochets = Load("Audio/Impacts/Ricochet");
        public readonly AudioClip[] Headshots = Load("Audio/Impacts/Headshot");
        public readonly AudioClip[] Bleeding = Load("Audio/Status/Bleeding");
        public readonly AudioClip[] BodyFalls = Load("Audio/Characters/BodyFall");
        public readonly AudioClip[] Footsteps = Load("Audio/Movement/Footsteps");

        static AudioClip[] Load(string path) => Resources.LoadAll<AudioClip>(path);
    }
}
