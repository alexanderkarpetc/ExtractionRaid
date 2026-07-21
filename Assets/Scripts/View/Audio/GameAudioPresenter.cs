using System.Collections.Generic;
using Adapters;
using Constants;
using Dev;
using Session;
using State;
using UnityEngine;

namespace View.Audio
{
    public sealed class GameAudioPresenter
    {
        const int PoolSize = 32;
        const float PistolMaxDistance = 55f;
        const float DistantBlendStart = 12f;
        const float DistantBlendEnd = 42f;
        const float WalkStepInterval = 0.5f;
        const float SprintStepInterval = 0.32f;
        const float MusicFadeDuration = 1.5f;

        sealed class Voice
        {
            public GameObject GameObject;
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public float StartedAt;
        }

        struct DelayedSound
        {
            public float PlayAt;
            public Vector3 Position;
            public AudioClip[] Clips;
            public float Volume;
        }

        readonly GameAudioClipLibrary _clips = new();
        readonly List<Voice> _voices = new(PoolSize);
        readonly List<DelayedSound> _delayed = new();
        readonly Dictionary<AudioClip[], int> _lastClipIndices = new();

        Transform _root;
        AudioSource _musicSource;
        float _nextStepTime;
        bool _rollSoundPlayed;

        public void LateTick(RaidSession session)
        {
            EnsurePool();
            TickMusic(session);
            TickDelayed();
            if (session == null || session.RaidState == null) return;

            var state = session.RaidState;
            var player = state.PlayerEntity;
            var listenerPosition = player != null ? player.Position : Vector3.zero;

            foreach (var e in session.ConsumeEvents().All)
                HandleEvent(e, state, player, listenerPosition);

            TickFootsteps(player);
        }

        void HandleEvent(RaidEvent e, RaidState state, PlayerEntityState player, Vector3 listenerPosition)
        {
            switch (e.Type)
            {
                case RaidEventType.WeaponFired:
                    if (e.StringPayload == "Ballistic" && e.DeliveryPattern == FiringPattern.Single)
                        PlayPistolShot(e.Position, listenerPosition);
                    break;
                case RaidEventType.WeaponDryFired:
                    if (IsPlayerPistol(player)) PlaySpatial(_clips.PistolDryFire, player.Position,
                        Volume(0.72f, Audio.DryFire), 18f);
                    break;
                case RaidEventType.WeaponReloadStarted:
                    if (IsPlayerPistol(player)) PlaySpatial(_clips.PistolReload, player.Position,
                        Volume(0.85f, Audio.Reload), 22f);
                    break;
                case RaidEventType.WeaponEquipStarted:
                    if (IsPendingOrEquippedPistol(player)) PlaySpatial(_clips.PistolUnholster, player.Position,
                        Volume(0.65f, Audio.Unholster), 18f);
                    break;
                case RaidEventType.WeaponUnequipStarted:
                    if (IsPlayerPistol(player)) PlaySpatial(_clips.PistolHolster, player.Position,
                        Volume(0.65f, Audio.Holster), 18f);
                    break;
                case RaidEventType.ProjectileHit:
                    PlaySpatial(e.StringPayload == "metal" ? _clips.MetalImpacts : _clips.HardSurfaceImpacts,
                        e.Position, Volume(0.82f, e.StringPayload == "metal"
                            ? Audio.MetalImpact : Audio.HardSurfaceImpact), 38f);
                    break;
                case RaidEventType.EntityHit:
                    PlayEntityHit(e);
                    break;
                case RaidEventType.StatusEffectApplied:
                    if (e.StringPayload == "Bleeding" && TryGetEntityPosition(state, e.Id, out var bleedPosition))
                        PlaySpatial(_clips.Bleeding, bleedPosition, Volume(0.72f, Audio.Bleeding), 24f);
                    break;
                case RaidEventType.EntityDied:
                    _delayed.Add(new DelayedSound
                    {
                        PlayAt = Time.unscaledTime + 0.42f,
                        Position = e.Position,
                        Clips = _clips.BodyFalls,
                        Volume = Volume(0.78f, Audio.BodyFall),
                    });
                    break;
            }
        }

        void PlayPistolShot(Vector3 position, Vector3 listenerPosition)
        {
            float distance = HorizontalDistance(position, listenerPosition);
            if (distance > PistolMaxDistance) return;

            float blend = Mathf.InverseLerp(DistantBlendStart, DistantBlendEnd, distance);
            bool occluded = Physics.Linecast(position, listenerPosition + Vector3.up,
                BotConstants.VisionBlockingMask, QueryTriggerInteraction.Ignore);
            float volume = occluded ? 0.65f : 1f;
            float cutoff = occluded ? 2600f : 22000f;

            PlaySpatial(_clips.PistolClose, position,
                Volume((1f - blend) * volume, Audio.CloseShot),
                PistolMaxDistance, 0.98f, 1.02f, cutoff);
            PlaySpatial(_clips.PistolDistant, position,
                Volume(blend * 0.9f * volume, Audio.DistantShot),
                PistolMaxDistance, 0.97f, 1.01f, cutoff);
        }

        void PlayEntityHit(RaidEvent e)
        {
            bool ricochet = e.KillerId.Value == 1;
            bool headshot = e.CurrentHp > 0.5f;
            float absorption = Mathf.Clamp01(e.Damage);
            if (ricochet)
            {
                PlaySpatial(_clips.Ricochets, e.Position, Volume(1f, Audio.Ricochet), 42f);
                return;
            }

            PlaySpatial(_clips.FleshImpacts, e.Position,
                Volume(Mathf.Clamp01(1f - absorption * 0.75f), Audio.FleshImpact), 32f);
            if (absorption > 0.08f)
                PlaySpatial(_clips.ArmorImpacts, e.Position, Volume(absorption, Audio.ArmorImpact), 36f);
            if (headshot)
                PlaySpatial(_clips.Headshots, e.Position, Volume(0.7f, Audio.Headshot), 36f);
        }

        void TickFootsteps(PlayerEntityState player)
        {
            if (player == null || player.IsInMenu)
            {
                _rollSoundPlayed = false;
                return;
            }
            if (player.IsRolling)
            {
                if (!_rollSoundPlayed)
                {
                    PlaySpatial(_clips.Footsteps, player.Position,
                        Volume(1f, Audio.SprintFootsteps), 22f, 0.88f, 0.94f);
                    _rollSoundPlayed = true;
                }
                return;
            }

            _rollSoundPlayed = false;
            if (new Vector2(player.Velocity.x, player.Velocity.z).sqrMagnitude < 0.16f) return;
            if (Time.time < _nextStepTime) return;

            bool sprinting = player.IsSprinting;
            PlaySpatial(_clips.Footsteps, player.Position,
                Volume(sprinting ? 0.9f : 0.68f,
                    sprinting ? Audio.SprintFootsteps : Audio.WalkFootsteps), 20f,
                sprinting ? 0.94f : 0.98f, sprinting ? 1.01f : 1.03f);
            _nextStepTime = Time.time + (sprinting ? SprintStepInterval : WalkStepInterval);
        }

        void TickDelayed()
        {
            float now = Time.unscaledTime;
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                var delayed = _delayed[i];
                if (now < delayed.PlayAt) continue;
                PlaySpatial(delayed.Clips, delayed.Position, delayed.Volume, 30f);
                _delayed.RemoveAt(i);
            }
        }

        void TickMusic(RaidSession session)
        {
            bool inHideout = session?.LevelState?.LevelId == MapIds.HideoutLevelId;
            if (inHideout && !_musicSource.isPlaying && _clips.HideoutMusic.Length > 0)
            {
                _musicSource.clip = _clips.HideoutMusic[0];
                _musicSource.volume = 0f;
                _musicSource.Play();
            }

            float targetVolume = inHideout ? Audio.Music : 0f;
            _musicSource.volume = Mathf.MoveTowards(_musicSource.volume, targetVolume,
                Time.unscaledDeltaTime / MusicFadeDuration);

            if (!inHideout && _musicSource.isPlaying && _musicSource.volume <= 0.001f)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }
        }

        void PlaySpatial(AudioClip[] clips, Vector3 position, float volume, float maxDistance,
            float minPitch = 0.97f, float maxPitch = 1.03f, float lowPassCutoff = 22000f)
        {
            if (clips == null || clips.Length == 0 || volume <= 0.001f) return;
            var clip = PickClip(clips);
            if (clip == null) return;

            var voice = AcquireVoice();
            voice.GameObject.transform.position = ListenerPlanePosition(position);
            voice.Source.clip = clip;
            voice.Source.volume = Mathf.Clamp01(volume);
            voice.Source.pitch = Random.Range(minPitch, maxPitch);
            voice.Source.maxDistance = maxDistance;
            voice.Source.minDistance = 2f;
            voice.LowPass.cutoffFrequency = lowPassCutoff;
            voice.Source.Play();
            voice.StartedAt = Time.unscaledTime;
        }

        AudioClip PickClip(AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];
            int previous = _lastClipIndices.TryGetValue(clips, out int value) ? value : -1;
            int index = Random.Range(0, clips.Length - 1);
            if (index >= previous) index++;
            _lastClipIndices[clips] = index;
            return clips[index];
        }

        Voice AcquireVoice()
        {
            Voice oldest = _voices[0];
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].Source.isPlaying) return _voices[i];
                if (_voices[i].StartedAt < oldest.StartedAt) oldest = _voices[i];
            }
            oldest.Source.Stop();
            return oldest;
        }

        void EnsurePool()
        {
            if (_root != null) return;
            var root = new GameObject("[GameAudio]");
            Object.DontDestroyOnLoad(root);
            _root = root.transform;

            _musicSource = root.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Voice_{i:00}");
                go.transform.SetParent(_root, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                var lowPass = go.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = 22000f;
                _voices.Add(new Voice { GameObject = go, Source = source, LowPass = lowPass });
            }
        }

        static bool IsPlayerPistol(PlayerEntityState player) => IsPistol(player?.EquippedWeapon);

        static bool IsPendingOrEquippedPistol(PlayerEntityState player)
        {
            if (player == null) return false;
            if (player.PendingHotbarSlot >= 0 && player.PendingHotbarSlot < player.Hotbar.Length)
                return IsPistol(player.Hotbar[player.PendingHotbarSlot]);
            return IsPistol(player.EquippedWeapon);
        }

        static bool IsPistol(WeaponEntityState weapon) =>
            weapon != null && weapon.PayloadDefinition?.Archetype == "Ballistic"
            && weapon.DeliveryDefinition?.Pattern == FiringPattern.Single;

        static bool TryGetEntityPosition(RaidState state, EId id, out Vector3 position)
        {
            if (state.PlayerEntity != null && state.PlayerEntity.Id == id)
            {
                position = state.PlayerEntity.Position;
                return true;
            }
            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Id != id) continue;
                position = state.Bots[i].Position;
                return true;
            }
            position = default;
            return false;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static Vector3 ListenerPlanePosition(Vector3 worldPosition)
        {
            var camera = Camera.main;
            if (camera != null) worldPosition.y = camera.transform.position.y;
            return worldPosition;
        }

        static ViewCheatsAudioSection Audio => ViewCheats.Config.Audio;

        static float Volume(float baseVolume, float effectMultiplier) =>
            baseVolume * Audio.MasterSfx * effectMultiplier;

        public void Dispose()
        {
            _delayed.Clear();
            _voices.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            _musicSource = null;
        }
    }
}
