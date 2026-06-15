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
    /// AttachmentEditorPresenter — pure edit-existing logic (Option B). Infinite mod
    /// supply from the registry; live edits to the weapon's WeaponConfiguration.Attachments.
    /// </summary>
    [TestFixture]
    public class AttachmentEditorPresenterTests
    {
        readonly List<Object> _cleanup = new();
        PayloadCoreDefinition  _payload;
        DeliveryCoreDefinition _delivery;
        ICoreDefinitionRegistry _registry;

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
            return new AttachmentEditorPresenter(_registry);
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

        // ── CompatibleMods ────────────────────────────────────

        [Test]
        public void CompatibleMods_ReturnsOnlyMatchingSlot()
        {
            var muzzle = MakeAtt("mz", AttachmentSlot.Muzzle, (WeaponStatAxis.Recoil, -10f));
            var mag    = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(muzzle, mag);
            p.Load(Weapon());

            var muzzleMods = p.CompatibleMods(AttachmentSlot.Muzzle);
            Assert.AreEqual(1, muzzleMods.Count);
            Assert.AreEqual("mz", muzzleMods[0].Id);
        }

        // ── Install / Remove ──────────────────────────────────

        [Test]
        public void Install_SetsInstalledIn_AndAffectsStats()
        {
            var mag = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());

            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);
            p.Install(AttachmentSlot.Magazine, "mg");

            Assert.IsTrue(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
            Assert.AreEqual("mg", p.InstalledIn(AttachmentSlot.Magazine).Value.DefinitionId);
            Assert.AreEqual(30, p.CurrentStats.Value.MagazineSize); // 20 * 1.5
        }

        [Test]
        public void Install_ReplacesExistingInSameSlot()
        {
            var a = MakeAtt("mgA", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var b = MakeAtt("mgB", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 100f));
            var p = PresenterWith(a, b);
            p.Load(Weapon());

            p.Install(AttachmentSlot.Magazine, "mgA");
            p.Install(AttachmentSlot.Magazine, "mgB");

            Assert.AreEqual("mgB", p.InstalledIn(AttachmentSlot.Magazine).Value.DefinitionId);
            Assert.AreEqual(40, p.CurrentStats.Value.MagazineSize); // only B applies: 20 * 2
        }

        [Test]
        public void Remove_ClearsSlot_AndRevertsStats()
        {
            var mag = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());

            p.Install(AttachmentSlot.Magazine, "mg");
            p.Remove(AttachmentSlot.Magazine);

            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);
        }

        [Test]
        public void Install_InvalidMod_NoOp()
        {
            var p = PresenterWith(); // empty registry
            p.Load(Weapon());
            p.Install(AttachmentSlot.Magazine, "ghost");
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
        }

        [Test]
        public void Install_WrongSlot_NoOp()
        {
            var mag = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());
            p.Install(AttachmentSlot.Muzzle, "mg"); // mag mod into muzzle slot
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Muzzle).HasValue);
        }

        // ── PreviewWith ───────────────────────────────────────

        [Test]
        public void PreviewWith_ReturnsModifiedStats_WithoutMutating()
        {
            var mag = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            p.Load(Weapon());

            var preview = p.PreviewWith(AttachmentSlot.Magazine, "mg");
            Assert.AreEqual(30, preview.Value.MagazineSize);             // candidate applied
            Assert.AreEqual(20, p.CurrentStats.Value.MagazineSize);      // actual unchanged
            Assert.IsFalse(p.InstalledIn(AttachmentSlot.Magazine).HasValue);
        }

        [Test]
        public void InstallAndRemove_BumpConfigVersion()
        {
            var mag = MakeAtt("mg", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var p = PresenterWith(mag);
            var w = Weapon();
            p.Load(w);
            int before = w.WeaponConfigVersion;

            p.Install(AttachmentSlot.Magazine, "mg");
            Assert.AreEqual(before + 1, w.WeaponConfigVersion);

            p.Remove(AttachmentSlot.Magazine);
            Assert.AreEqual(before + 2, w.WeaponConfigVersion);
        }
    }
}
