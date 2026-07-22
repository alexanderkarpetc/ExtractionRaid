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
        const float RifleMaxDistance = 70f;
        const float ShotgunMaxDistance = 65f;
        const float DistantBlendStart = 12f;
        const float DistantBlendEnd = 42f;
        const float OccludedNearVolume = 0.88f;
        const float OccludedFarVolume = 0.65f;
        const float OccludedNearCutoff = 9000f;
        const float OccludedFarCutoff = 2600f;
        const float WalkStepInterval = 0.5f;
        const float SprintStepInterval = 0.32f;
        const float MusicFadeDuration = 1.5f;
        const float BotVoiceMaxDistance = 36f;
        const float LostVisualDelay = 1.25f;
        const float NearbyDeathCalloutRange = 22f;

        enum BotVoiceFaction : byte
        {
            None,
            Pmc,
            Scav,
        }

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

        sealed class BotVoiceMemory
        {
            public BotVoiceFaction Faction;
            public Vector3 Position;
            public bool HasTarget;
            public bool CanSeeTarget;
            public float LastFireTime;
            public WeaponPhase WeaponPhase;
            public float LostSightAt = -1f;
            public bool LostVisualOffered;
            public float NextAmbientTime;
            public float NextVoiceTime;
        }

        struct BotVoiceCandidate
        {
            public EId SpeakerId;
            public BotVoiceFaction Faction;
            public AudioClip[] Clips;
            public Vector3 Position;
            public Vector3 ListenerPosition;
            public int Priority;
            public float DistanceSq;
        }

        readonly GameAudioClipLibrary _clips = new();
        readonly List<Voice> _voices = new(PoolSize);
        readonly List<DelayedSound> _delayed = new();
        readonly Dictionary<AudioClip[], int> _lastClipIndices = new();
        readonly Dictionary<EId, BotVoiceMemory> _botVoiceMemories = new();
        readonly List<EId> _staleBotVoiceIds = new();

        Transform _root;
        AudioSource _musicSource;
        float _nextStepTime;
        bool _rollSoundPlayed;
        bool _inventoryWasOpen;
        bool _canOfferBotVoice;
        bool _hasBotVoiceCandidate;
        BotVoiceCandidate _botVoiceCandidate;
        float _botVoicePlayingUntil;
        float _nextBotVoiceTime;

        public void LateTick(RaidSession session)
        {
            EnsurePool();
            TickMusic(session);
            TickDelayed();
            if (session == null || session.RaidState == null)
            {
                _inventoryWasOpen = false;
                _botVoiceMemories.Clear();
                return;
            }

            var state = session.RaidState;
            var player = state.PlayerEntity;
            var listenerPosition = player != null ? player.Position : Vector3.zero;

            var events = session.ConsumeEvents().All;
            foreach (var e in events)
                HandleEvent(e, state, player, listenerPosition);

            TickBotVoices(state, player, events, listenerPosition);
            TickBackpack(player);
            TickFootsteps(player);
        }

        void HandleEvent(RaidEvent e, RaidState state, PlayerEntityState player, Vector3 listenerPosition)
        {
            switch (e.Type)
            {
                case RaidEventType.WeaponFired:
                    if (e.StringPayload == "Ballistic")
                    {
                        if (e.DeliveryPattern == FiringPattern.Single)
                            PlayPistolShot(e.Position, listenerPosition);
                        else if (e.DeliveryPattern == FiringPattern.Auto)
                            PlayWeaponShot(_clips.RifleFire, e.Position, listenerPosition,
                                RifleMaxDistance, Audio.RifleShot);
                        else if (e.DeliveryPattern == FiringPattern.Scatter)
                            PlayWeaponShot(_clips.ShotgunFire, e.Position, listenerPosition,
                                ShotgunMaxDistance, Audio.ShotgunShot);
                    }
                    break;
                case RaidEventType.WeaponDryFired:
                    if (IsPlayerPistol(player))
                        PlaySpatial(_clips.PistolDryFire, player.Position,
                            Volume(0.72f, Audio.DryFire), 18f);
                    else if (IsPlayerRifle(player))
                        PlaySpatial(_clips.RifleDryFire, player.Position,
                            Volume(0.75f, Audio.RifleDryFire), 18f);
                    break;
                case RaidEventType.WeaponReloadStarted:
                    if (IsPlayerPistol(player))
                        PlaySpatial(_clips.PistolReload, player.Position,
                            Volume(0.85f, Audio.Reload), 22f);
                    else if (IsPlayerRifle(player))
                        PlaySpatial(_clips.RifleReload, player.Position,
                            Volume(0.85f, Audio.RifleReload), 22f, 1f, 1f);
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
            ResolveShotOcclusion(position, listenerPosition, distance, out float volume, out float cutoff);

            PlaySpatial(_clips.PistolClose, position,
                Volume((1f - blend) * volume, Audio.CloseShot),
                PistolMaxDistance, 0.98f, 1.02f, cutoff);
            PlaySpatial(_clips.PistolDistant, position,
                Volume(blend * 0.9f * volume, Audio.DistantShot),
                PistolMaxDistance, 0.97f, 1.01f, cutoff);
        }

        void PlayWeaponShot(AudioClip[] clips, Vector3 position, Vector3 listenerPosition,
            float maxDistance, float volumeMultiplier)
        {
            float distance = HorizontalDistance(position, listenerPosition);
            if (distance > maxDistance) return;

            ResolveShotOcclusion(position, listenerPosition, distance, out float volume, out float cutoff);
            PlaySpatial(clips, position, Volume(volume, volumeMultiplier), maxDistance,
                0.98f, 1.02f, cutoff);
        }

        static void ResolveShotOcclusion(Vector3 position, Vector3 listenerPosition, float distance,
            out float volume, out float cutoff)
        {
            bool occluded = Physics.Linecast(position, listenerPosition + Vector3.up,
                BotConstants.VisionBlockingMask, QueryTriggerInteraction.Ignore);
            volume = 1f;
            cutoff = 22000f;
            if (!occluded) return;

            float occlusionDistance = Mathf.InverseLerp(DistantBlendStart, DistantBlendEnd, distance);
            volume = Mathf.Lerp(OccludedNearVolume, OccludedFarVolume, occlusionDistance);
            cutoff = Mathf.Lerp(OccludedNearCutoff, OccludedFarCutoff, occlusionDistance);
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

        void TickBotVoices(RaidState state, PlayerEntityState player,
            IReadOnlyList<RaidEvent> events, Vector3 listenerPosition)
        {
            float now = Time.unscaledTime;
            _canOfferBotVoice = now >= _botVoicePlayingUntil && now >= _nextBotVoiceTime;
            _hasBotVoiceCandidate = false;

            ProcessBotVoiceEvents(state, player, events, listenerPosition);

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                var faction = GetBotVoiceFaction(bot.TypeId);
                if (faction == BotVoiceFaction.None) continue;

                if (!_botVoiceMemories.TryGetValue(bot.Id, out var memory))
                {
                    memory = new BotVoiceMemory
                    {
                        Faction = faction,
                        Position = bot.Position,
                        LastFireTime = bot.Weapon?.LastFireTime ?? -999f,
                        WeaponPhase = bot.Weapon?.Phase ?? WeaponPhase.Ready,
                        NextAmbientTime = now + Random.Range(12f, 22f),
                    };
                    _botVoiceMemories.Add(bot.Id, memory);
                }

                var bb = bot.Blackboard;
                if (bb.CanSeeTarget && !memory.CanSeeTarget)
                {
                    OfferBotVoice(bot.Id, faction,
                        faction == BotVoiceFaction.Pmc ? _clips.PmcContact : _clips.ScavContact,
                        bot.Position, priority: 70, listenerPosition);
                    memory.LostSightAt = -1f;
                    memory.LostVisualOffered = false;
                }
                else if (bb.HasTarget && !bb.CanSeeTarget && !memory.HasTarget)
                {
                    OfferBotVoice(bot.Id, faction,
                        faction == BotVoiceFaction.Pmc ? _clips.PmcHeardSomething : _clips.ScavHeardSomething,
                        bot.Position, priority: 65, listenerPosition);
                }

                if (memory.CanSeeTarget && !bb.CanSeeTarget)
                {
                    memory.LostSightAt = now;
                    memory.LostVisualOffered = false;
                }
                else if (bb.CanSeeTarget)
                {
                    memory.LostSightAt = -1f;
                    memory.LostVisualOffered = false;
                }

                if (faction == BotVoiceFaction.Pmc && memory.LostSightAt >= 0f
                    && !memory.LostVisualOffered && now - memory.LostSightAt >= LostVisualDelay)
                {
                    if (OfferBotVoice(bot.Id, faction, _clips.PmcLostVisual,
                        bot.Position, priority: 55, listenerPosition))
                        memory.LostVisualOffered = true;
                }

                float lastFireTime = bot.Weapon?.LastFireTime ?? -999f;
                if (lastFireTime > memory.LastFireTime && Random.value < 0.5f)
                {
                    OfferBotVoice(bot.Id, faction,
                        faction == BotVoiceFaction.Pmc ? _clips.PmcCoverMe : _clips.ScavHelp,
                        bot.Position, priority: 45, listenerPosition);
                }

                var weaponPhase = bot.Weapon?.Phase ?? WeaponPhase.Ready;
                if (weaponPhase == WeaponPhase.Reloading && memory.WeaponPhase != WeaponPhase.Reloading
                    && Random.value < 0.9f)
                {
                    OfferBotVoice(bot.Id, faction,
                        faction == BotVoiceFaction.Pmc ? _clips.PmcReloading : _clips.ScavReloading,
                        bot.Position, priority: 60, listenerPosition);
                }

                if (faction == BotVoiceFaction.Pmc && now >= memory.NextAmbientTime)
                {
                    memory.NextAmbientTime = now + Random.Range(18f, 35f);
                    if (!bb.HasTarget)
                    {
                        bool patrolling = bb.DebugStatus != null
                                          && bb.DebugStatus.StartsWith("Patrol")
                                          && bb.PatrolWaitTimer <= 0f;
                        OfferBotVoice(bot.Id, faction,
                            patrolling ? _clips.PmcMoving : _clips.PmcStaySharp,
                            bot.Position, priority: 20, listenerPosition);
                    }
                }

                memory.Faction = faction;
                memory.Position = bot.Position;
                memory.HasTarget = bb.HasTarget;
                memory.CanSeeTarget = bb.CanSeeTarget;
                memory.LastFireTime = lastFireTime;
                memory.WeaponPhase = weaponPhase;
            }

            RemoveStaleBotVoiceMemories(state);
            if (_hasBotVoiceCandidate)
                PlayBotVoice(in _botVoiceCandidate, now);
        }

        void ProcessBotVoiceEvents(RaidState state, PlayerEntityState player,
            IReadOnlyList<RaidEvent> events, Vector3 listenerPosition)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == RaidEventType.EntityHit && e.MaxHp < 0.5f
                    && TryGetBotVoiceIdentity(state, e.Id, out var hitFaction, out var hitPosition))
                {
                    var clips = hitFaction == BotVoiceFaction.Pmc
                        ? _clips.PmcHit
                        : e.CurrentHp > 0.5f ? _clips.ScavHeadshot : _clips.ScavHit;
                    if (Random.value < (e.CurrentHp > 0.5f ? 0.8f : 0.5f))
                        OfferBotVoice(e.Id, hitFaction, clips, hitPosition,
                            priority: e.CurrentHp > 0.5f ? 85 : 75, listenerPosition);
                    continue;
                }

                if (e.Type == RaidEventType.GrenadeSpawned)
                {
                    OfferNearestBotVoice(state, BotVoiceFaction.Pmc, e.Position, 28f,
                        _clips.PmcGrenade, priority: 95, listenerPosition);
                    continue;
                }

                if (e.Type != RaidEventType.EntityDied) continue;
                if (player != null && e.Id == player.Id)
                {
                    OfferNearestBotVoice(state, BotVoiceFaction.Pmc, player.Position, float.MaxValue,
                        _clips.PmcAreaSecure, priority: 100, listenerPosition);
                    OfferNearestBotVoice(state, BotVoiceFaction.Scav, player.Position, float.MaxValue,
                        _clips.ScavCheckPockets, priority: 100, listenerPosition);
                    continue;
                }

                if (!TryGetBotVoiceIdentity(state, e.Id, out var deadFaction, out var deadPosition))
                    continue;
                OfferNearestBotVoice(state, deadFaction, deadPosition, NearbyDeathCalloutRange,
                    deadFaction == BotVoiceFaction.Pmc ? _clips.PmcManDown : _clips.ScavBastard,
                    priority: 90, listenerPosition, excludedId: e.Id);
            }
        }

        void OfferNearestBotVoice(RaidState state, BotVoiceFaction faction, Vector3 origin,
            float maxDistance, AudioClip[] clips, int priority, Vector3 listenerPosition,
            EId excludedId = default)
        {
            BotEntityState nearest = null;
            float nearestDistanceSq = maxDistance * maxDistance;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                if (bot.Id == excludedId || GetBotVoiceFaction(bot.TypeId) != faction) continue;
                float distanceSq = HorizontalDistanceSq(bot.Position, origin);
                if (distanceSq > nearestDistanceSq) continue;
                nearest = bot;
                nearestDistanceSq = distanceSq;
            }
            if (nearest != null)
                OfferBotVoice(nearest.Id, faction, clips, nearest.Position, priority, listenerPosition);
        }

        bool OfferBotVoice(EId speakerId, BotVoiceFaction faction, AudioClip[] clips,
            Vector3 position, int priority, Vector3 listenerPosition)
        {
            if (!_canOfferBotVoice || clips == null || clips.Length == 0) return false;
            if (_botVoiceMemories.TryGetValue(speakerId, out var memory)
                && Time.unscaledTime < memory.NextVoiceTime) return false;

            float distanceSq = HorizontalDistanceSq(position, listenerPosition);
            if (distanceSq > BotVoiceMaxDistance * BotVoiceMaxDistance) return false;
            if (_hasBotVoiceCandidate && (priority < _botVoiceCandidate.Priority
                || priority == _botVoiceCandidate.Priority && distanceSq >= _botVoiceCandidate.DistanceSq))
                return false;

            _botVoiceCandidate = new BotVoiceCandidate
            {
                SpeakerId = speakerId,
                Faction = faction,
                Clips = clips,
                Position = position,
                ListenerPosition = listenerPosition,
                Priority = priority,
                DistanceSq = distanceSq,
            };
            _hasBotVoiceCandidate = true;
            return true;
        }

        void PlayBotVoice(in BotVoiceCandidate candidate, float now)
        {
            var clip = PickClip(candidate.Clips);
            if (clip == null) return;

            float distance = Mathf.Sqrt(candidate.DistanceSq);
            ResolveShotOcclusion(candidate.Position, candidate.ListenerPosition, distance,
                out float occlusionVolume, out float cutoff);
            float categoryVolume = candidate.Faction == BotVoiceFaction.Pmc
                ? Audio.PmcVoice : Audio.ScavVoice;

            var voice = AcquireVoice();
            voice.GameObject.transform.position = ListenerPlanePosition(candidate.Position);
            voice.Source.clip = clip;
            voice.Source.volume = Mathf.Clamp01(Volume(occlusionVolume, categoryVolume));
            voice.Source.pitch = Random.Range(0.98f, 1.02f);
            voice.Source.maxDistance = BotVoiceMaxDistance;
            voice.Source.minDistance = 2f;
            voice.LowPass.cutoffFrequency = cutoff;
            voice.Source.Play();
            voice.StartedAt = now;

            _botVoicePlayingUntil = now + clip.length / voice.Source.pitch;
            _nextBotVoiceTime = _botVoicePlayingUntil + Random.Range(2f, 3f);
            if (_botVoiceMemories.TryGetValue(candidate.SpeakerId, out var memory))
                memory.NextVoiceTime = _botVoicePlayingUntil + (candidate.Faction == BotVoiceFaction.Pmc
                    ? Random.Range(4f, 8f)
                    : Random.Range(3f, 6f));
        }

        bool TryGetBotVoiceIdentity(RaidState state, EId id,
            out BotVoiceFaction faction, out Vector3 position)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Id != id) continue;
                faction = GetBotVoiceFaction(state.Bots[i].TypeId);
                position = state.Bots[i].Position;
                return faction != BotVoiceFaction.None;
            }
            if (_botVoiceMemories.TryGetValue(id, out var memory))
            {
                faction = memory.Faction;
                position = memory.Position;
                return faction != BotVoiceFaction.None;
            }
            faction = BotVoiceFaction.None;
            position = default;
            return false;
        }

        void RemoveStaleBotVoiceMemories(RaidState state)
        {
            _staleBotVoiceIds.Clear();
            foreach (var pair in _botVoiceMemories)
            {
                bool found = false;
                for (int i = 0; i < state.Bots.Count; i++)
                {
                    if (state.Bots[i].Id != pair.Key) continue;
                    found = true;
                    break;
                }
                if (!found) _staleBotVoiceIds.Add(pair.Key);
            }
            for (int i = 0; i < _staleBotVoiceIds.Count; i++)
                _botVoiceMemories.Remove(_staleBotVoiceIds[i]);
        }

        static BotVoiceFaction GetBotVoiceFaction(string typeId)
        {
            if (typeId == BotConstants.PMC.TypeId) return BotVoiceFaction.Pmc;
            if (typeId == BotConstants.Scav.TypeId) return BotVoiceFaction.Scav;
            return BotVoiceFaction.None;
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

        void TickBackpack(PlayerEntityState player)
        {
            if (player == null)
            {
                _inventoryWasOpen = false;
                return;
            }

            if (player.IsInventoryOpen && !_inventoryWasOpen)
                PlaySpatial(_clips.BackpackOpen, player.Position,
                    Volume(0.75f, Audio.BackpackOpen), 18f, 0.98f, 1.02f);
            _inventoryWasOpen = player.IsInventoryOpen;
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

        static bool IsPlayerRifle(PlayerEntityState player) => IsRifle(player?.EquippedWeapon);

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

        static bool IsRifle(WeaponEntityState weapon) =>
            weapon != null && weapon.PayloadDefinition?.Archetype == "Ballistic"
            && weapon.DeliveryDefinition?.Pattern == FiringPattern.Auto;

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
            return Mathf.Sqrt(HorizontalDistanceSq(a, b));
        }

        static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
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
            _botVoiceMemories.Clear();
            _staleBotVoiceIds.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            _musicSource = null;
            _inventoryWasOpen = false;
            _botVoicePlayingUntil = 0f;
            _nextBotVoiceTime = 0f;
        }
    }
}
