using System.Collections.Generic;
using ApplicationCore;
using Dev;
using Session;
using State;
using Systems;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Spawns an always-on <see cref="WorldBeacon"/> at each <see cref="DeployPointState"/>
    /// in the HIDEOUT, so a new player can see where to leave the bunker and head out on a
    /// raid (exit-to-map wayfinding). Only active in the hideout; cleared elsewhere.
    /// Tunable via ViewCheats → 🧭 Deploy Marker. View-only MonoBehaviour on the App GO
    /// (mirrors <see cref="ExtractionHudPresenter"/>); self-ticks, reads RaidState.
    /// </summary>
    public class DeployBeaconPresenter : MonoBehaviour
    {
        readonly List<WorldBeacon> _beacons = new();
        RaidSession _session;
        bool _built;

        void Update()
        {
            if (!App.IsInitialized) { Clear(); return; }

            var app = App.Instance;
            var session = app.RaidSession;

            if (session != _session)
            {
                Clear();
                _session = session;
            }

            // Deploy points live only in the hideout (exit-to-raid). No beacons in raids,
            // and gated behind accepting the first quest (onboarding).
            if (session == null || !app.IsInHideout
                || !QuestSystem.HasAcceptedAnyQuest(app.Player?.QuestProgress))
            { Clear(); return; }

            var state = session.RaidState;
            if (!_built && state != null && state.DeployPoints.Count > 0)
            {
                Build(state);
                _built = true;
            }

            var cfg = ViewCheats.Config?.DeployMarker;
            if (cfg != null)
                for (int i = 0; i < _beacons.Count; i++)
                    if (_beacons[i] != null) Apply(_beacons[i], cfg);
        }

        void Build(RaidState state)
        {
            var cfg = ViewCheats.Config?.DeployMarker;
            for (int i = 0; i < state.DeployPoints.Count; i++)
            {
                var dp = state.DeployPoints[i];
                var beacon = WorldBeacon.Create(dp.Position, $"DeployBeacon_{dp.Id.Value}");
                if (cfg != null) Apply(beacon, cfg);
                _beacons.Add(beacon);
            }
        }

        static void Apply(WorldBeacon b, ViewCheatsDeployMarkerSection c)
        {
            b.Color = c.Color;
            b.GroundRadius = c.GroundRadius;
            b.GroundY = c.GroundY;
            b.GroundSoftFade = c.GroundSoftFade;
            b.GroundAlphaMin = c.GroundAlphaMin;
            b.GroundAlphaMax = c.GroundAlphaMax;
            b.BeamHeight = c.BeamHeight;
            b.BeamHalfWidth = c.BeamHalfWidth;
            b.BeamBaseY = c.BeamBaseY;
            b.BeamAlphaMin = c.BeamAlphaMin;
            b.BeamAlphaMax = c.BeamAlphaMax;
            b.PulseHz = c.PulseHz;
        }

        void Clear()
        {
            for (int i = 0; i < _beacons.Count; i++)
                if (_beacons[i] != null) Destroy(_beacons[i].gameObject);
            _beacons.Clear();
            _built = false;
        }

        void OnDestroy() => Clear();
    }
}
