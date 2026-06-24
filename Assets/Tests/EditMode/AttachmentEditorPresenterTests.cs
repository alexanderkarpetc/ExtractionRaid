using System.Collections.Generic;
using Adapters;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;
using View.UI.Attachments;

namespace Tests.EditMode
{
    /// <summary>
    /// AttachmentEditorPresenter — edit-existing logic (Option B), loot-gated supply.
    /// Mods must be owned in the backpack to install; installing consumes one unit,
    /// removing/swapping returns the displaced mod (recoverable, blocked when full).
    ///
    /// Test mod ids deliberately reuse the real attachment item ids (ExtendedMag/QuickMag/
    /// MuzzleBrake/RedDot) so InventorySystem.AddToBackpack — which resolves stack size via
    /// ItemDefinition.Get — works on the return path exactly as it does in-game.
    /// </summary>
    [TestFixture]
    public class AttachmentEditorPresenterTests
    {
        const string Mag1   = "ExtendedMag";  // Magazine slot
        const string Mag2   = "QuickMag";     // Magazine slot
        const string Muzzle = "MuzzleBrake";  // Muzzle slot
        const string Optic  = "RedDot";       // Optic slot

        readonly List<Object> _cleanup = new();
        PayloadCoreDefinition  _payload;
        DeliveryCoreDefinition _delivery;
        ICoreDefinitionRegistry _registry;
        InventoryState _inv;
        int _nextEid = 100;

        EId AllocEId() => new EId(_nextEid++);

        [SetUp]
        public void SetUp()
        {
            _payload  = WeaponBuilderTestFactory.MakeBallistic(id: "p",
                commonStats: new CommonPayloadStats { Damage = 10f });
            _delivery = WeaponBuilderTestFactory.MakeDelivery(id: "d",
                commonStats: new DeliveryStats { FireInterval = 0.5f, MagazineSize = 20 });
            _cleanup.Add(_payload);
            _cleanup.Add(_delivery);
        }

        [TearDown]
        public void TearDown()
        {
            WeaponBuilderTestFactory.DestroyAll(_cleanup.ToArray());
            _cleanup.Clear();
        }

        AttachmentDefinition MakeAtt(string id, AttachmentSlot slot,
                                     params (WeaponStatAxis axis, float pct)[] deltas)
        {
            var def = ScriptableObject.CreateInstance<AttachmentDefinition>();
            WeaponBuilderTestFactory.SetPrivateField(def, "_id", id);
            WeaponBuilderTestFactory.SetPrivateField(def, "_slot", slot);
            var mods = new StatDelta[deltas.Length];
            for (int i = 0; i < deltas.Length; i++)
                mods[i] = new StatDelta { Axis = deltas[i].axis, Percent = deltas[i].pct };
            WeaponBuilderTestFactory.SetPrivateField(def, "_modifiers", mods);
            _cleanup.Add(def);
            return def;
        }

        AttachmentEditorPresenter PresenterWith(params AttachmentDefinition[] atts)
        {
            var db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            db.SetEntries(
                new List<PayloadCoreDefinition> { _payload },
                new List<DeliveryCoreDefinition> { _delivery },
                null,
                new List<AttachmentDefinition>(atts));
            _cleanup.Add(db);
            _registry = new DatabaseCoreDefinitionRegistry(db);
            _inv = new InventoryState();
            return new AttachmentEditorPresenter(_registry, _inv, AllocEId);
        }

        // Put `count` units of a mod into the backpack (loot the player owns).
        void Stock(string modId, int count)
        {
            int slot = _inv.FindFreeBackpackSlot();
            Assert.GreaterOrEqual(slot, 0, "test backpack overflow");
            _inv.Backpack[slot] = ItemState.Create(AllocEId(), modId, count);
        }

        // Fill every remaining free backpack slot with a non-mod filler so returns are blocked.
        void FillBackpack(string fillerId = "Filler")
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (_inv.Backpack[i] == null)
                    _inv.Backpack[i] = ItemState.Create(AllocEId(), fillerId, 1);
        }

        static ItemState Weapon() => ItemState.CreateWeapon(new EId(1), "Weapon",
            new WeaponConfiguration(
                new PayloadCoreInstance("p", RarityTier.Common),
                new DeliveryCoreInstance("d", RarityTier.Common),
                exotic: null, ammoInMagazine: 20));

        // ── Load ──────────────────────────────────────────────

        [Test]
        public void Load_NonWeapon_HasWeaponFalse()
        {
            var p = PresenterWith();
            p.Load(ItemState.Create(new EId(2), "Medkit"));
            Assert.IsFalse(p.HasWeapon);
        }

        [Test]
        public void Load_Weapon_HasWeaponTrue_FiresStateChanged()
        {
            var p = PresenterWith();
            int fired = 0;
            p.StateChanged += () => fired++;
            p.Load(Weapon());
            Assert.IsTrue(p.HasWeapon);
            Assert.AreEqual(1, fired);
        }

        // ── CompatibleMods (loot-gated) ───────────────────────

        [Test]
        public void CompatibleMods_ReturnsOnlyMatchingSlot()
        {
            var muzzle = MakeAtt(Muzzle, AttachmentSlot.Muzzle, (WeaponStatAxis.Recoil, -10f));
            var mag    = MakeAtt(Mag1,   AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(muzzle, mag);
            p.Load(Weapon());
            Stock(Muzzle, 1);
            Stock(Mag1, 1);

            var muzzleMods = p.CompatibleMods(AttachmentSlot.Muzzle);
            Assert.AreEqual(1, muzzleMods.Count);
            Assert.AreEqual(Muzzle, muzzleMods[0].Id);
        }

        [Test]
        public void CompatibleMods_OnlyOwned_ButInstalledAlwaysShows()
        {
            var owned   = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var notOwned = MakeAtt(Mag2, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 100f));
            var p = PresenterWith(owned, notOwned);
            p.Load(Weapon());
            Stock(Mag1, 1); // only Mag1 owned

            var before = p.CompatibleMods(AttachmentSlot.Magazine);
            CollectionAssert.AreEquivalent(new[] { Mag1 }, Ids(before));

            // After installing Mag1 it leaves the backpack, but it must still appear so it
            // can be removed/swapped.
            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            var after = p.CompatibleMods(AttachmentSlot.Magazine);
            CollectionAssert.AreEquivalent(new[] { Mag1 }, Ids(after));
        }

        static string[] Ids(IReadOnlyList<AttachmentDefinition> defs)
        {
            var r = new string[defs.Count];
            for (int i = 0; i < defs.Count; i++) r[i] = defs[i].Id;
            return r;
        }

        // ── Install / Remove ──────────────────────────────────

        [Test]
        public void Install_SetsInstalledIn_AndAffectsStats()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 1);

            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);
            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));

            Assert.IsTrue(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
            Assert.AreEqual(Mag1, p.InstalledIn(AttachmentSlot.Magazine).Value.DefinitionId);
            Assert.AreEqual(30, p.CurrentStats.Value.MagazineSize); // 20 * 1.5
        }

        [Test]
        public void Install_ConsumesOneFromBackpack()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 2);

            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.AreEqual(1, p.CountInBackpack(Mag1)); // one consumed
        }

        [Test]
        public void Install_NotOwned_NoOp()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag); // in registry but not stocked
            p.Load(Weapon());

            Assert.IsFalse(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
        }

        [Test]
        public void Remove_ReturnsToBackpack_AndRevertsStats()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 1);

            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.AreEqual(0, p.CountInBackpack(Mag1));

            Assert.IsTrue(p.Remove(AttachmentSlot.Magazine));
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
            Assert.AreEqual(1, p.CountInBackpack(Mag1)); // returned
            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);
        }

        [Test]
        public void Remove_BackpackFull_Blocked()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 1);
            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1)); // consumed → slot freed
            FillBackpack();                                          // no room to return

            Assert.IsFalse(p.Remove(AttachmentSlot.Magazine));
            Assert.IsTrue(p.InstalledIn(AttachmentSlot.Magazine).HasValue, "mod stays installed when return is blocked");
            Assert.IsNotNull(p.LastError);
        }

        [Test]
        public void Install_SwapReturnsDisplacedToBackpack()
        {
            var a = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var b = MakeAtt(Mag2, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 100f));
            var p = PresenterWith(a, b);
            p.Load(Weapon());
            Stock(Mag1, 1);
            Stock(Mag2, 1);

            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag2)); // swap

            Assert.AreEqual(Mag2, p.InstalledIn(AttachmentSlot.Magazine).Value.DefinitionId);
            Assert.AreEqual(1, p.CountInBackpack(Mag1)); // displaced mod returned
            Assert.AreEqual(0, p.CountInBackpack(Mag2)); // new mod consumed
            Assert.AreEqual(40, p.CurrentStats.Value.MagazineSize); // only B applies: 20 * 2
        }

        [Test]
        public void Install_AlreadyInstalled_NoOp()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 2);

            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.IsFalse(p.Install(AttachmentSlot.Magazine, Mag1)); // same mod again — no-op
            Assert.AreEqual(1, p.CountInBackpack(Mag1));              // not consumed twice
        }

        [Test]
        public void Install_InvalidMod_NoOp()
        {
            var p = PresenterWith(); // empty registry
            p.Load(Weapon());
            Assert.IsFalse(p.Install(AttachmentSlot.Magazine, "ghost"));
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
        }

        [Test]
        public void Install_WrongSlot_NoOp()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            Stock(Mag1, 1);
            Assert.IsFalse(p.Install(AttachmentSlot.Muzzle, Mag1)); // mag mod into muzzle slot
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Muzzle).HasValue);
        }

        // ── PreviewWith ───────────────────────────────────────

        [Test]
        public void PreviewWith_ReturnsModifiedStats_WithoutMutating()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());

            var preview = p.PreviewWith(AttachmentSlot.Magazine, Mag1);
            Assert.AreEqual(30, preview.Value.MagazineSize);             // candidate applied
            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);      // actual unchanged
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
        }

        [Test]
        public void InstallAndRemove_BumpConfigVersion()
        {
            var mag = MakeAtt(Mag1, AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            var w = Weapon();
            p.Load(w);
            Stock(Mag1, 1);
            int before = w.WeaponConfigVersion;

            Assert.IsTrue(p.Install(AttachmentSlot.Magazine, Mag1));
            Assert.AreEqual(before + 1, w.WeaponConfigVersion);

            Assert.IsTrue(p.Remove(AttachmentSlot.Magazine));
            Assert.AreEqual(before + 2, w.WeaponConfigVersion);
        }
    }
}
