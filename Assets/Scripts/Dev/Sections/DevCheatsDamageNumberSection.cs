using UnityEngine;

namespace Dev
{
    public class DevCheatsDamageNumberSection : ScriptableObject
    {
        public bool Enabled = true;
        // 0=FloatUp, 1=Knockback (opposite to bullet), 2=ArcGravity, 3=Scatter random
        public int TrajectoryMode = 1;
        public float Duration = 0.8f;
        public float FlySpeed = 80f;
        public float GravityAccel = 200f; // for Arc mode
        public float PopDuration = 0.15f;
        public float PopOvershoot = 1.3f;
        public float BaseFontSize = 18f;
        public float DamageScaleFactor = 10f;
        public float RandomSpread = 20f; // random angular spread (degrees) for knockback/scatter
        public Color NormalColor = Color.white;
        public Color HeadshotColor = new(1f, 0.85f, 0.2f, 1f);
        public Color KillColor = new(1f, 0.15f, 0.15f, 1f);
        public Color ArmorAbsorbColor = new(0.55f, 0.55f, 0.6f, 1f);
    }
}
