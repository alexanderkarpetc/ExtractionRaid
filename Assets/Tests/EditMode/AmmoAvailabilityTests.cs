using System.Collections.Generic;
using System.Linq;
using Constants;
using NUnit.Framework;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Ammo audit (2026-07-27). A weapon can only ever chamber the exact ammo id its payload
    /// core declares — <see cref="Systems.AmmoSystem"/> matches on <c>DefinitionId</c>, there is
    /// no variant substitution. So every ammo the game hands out (shop stock, container drops,
    /// craft output) must be an id some payload reads, otherwise the player is collecting dead
    /// weight. That was real: the starting Ammo Box shipped 30-40 Ammo_Pistol rounds no weapon
    /// could load, PMC bots dropped Ammo_Rifle_AP, and Rifle AP was craftable for
    /// Military_Components.
    ///
    /// These tests read the SHIPPED assets, not fixtures — they are here to catch a config
    /// authored against a caliber the Weapon Builder doesn't have (yet). When ammo selection
    /// lands and AP/HP become loadable, the usable set grows on its own and nothing here needs
    /// editing.
    /// </summary>
    [TestFixture]
    public class AmmoAvailabilityTests
    {
        static HashSet<string> _usable;

        /// <summary>Ammo ids some payload core can actually chamber.</summary>
        static HashSet<string> UsableAmmoIds()
        {
            if (_usable != null) return _usable;

            var db = Resources.Load<CoreDefinitionDatabase>("WeaponBuilder/CoreDefinitionDatabase");
            Assert.IsNotNull(db, "CoreDefinitionDatabase missing — run Tools → Weapon Builder → Create Stub Assets.");

            _usable = new HashSet<string>();
            foreach (var payload in db.Payloads)
                if (payload != null && !string.IsNullOrEmpty(payload.AmmoType))
                    _usable.Add(payload.AmmoType);

            Assert.IsNotEmpty(_usable, "No payload declares an AmmoType — the audit below would pass vacuously.");
            return _usable;
        }

        static bool IsAmmo(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) return false;
            var def = ItemDefinition.Get(definitionId);
            return def != null && def.Category == ItemCategory.Ammo;
        }

        [Test]
        public void ShopStock_OffersOnlyChamberableAmmo()
        {
            var usable = UsableAmmoIds();

            foreach (var shop in Resources.LoadAll<ShopDefinitionAsset>("Configs/Shops"))
            {
                if (shop.Stock == null) continue;
                foreach (var entry in shop.Stock)
                {
                    if (!IsAmmo(entry?.ItemDefId)) continue;
                    Assert.Contains(entry.ItemDefId, usable.ToList(),
                        $"Shop '{shop.name}' sells {entry.ItemDefId}, which no payload can chamber.");
                }
            }
        }

        [Test]
        public void ContainerDrops_OfferOnlyChamberableAmmo()
        {
            var usable = UsableAmmoIds();

            foreach (var asset in Resources.LoadAll<ContainerTypeConfigAsset>("Configs/Containers"))
            {
                var config = asset.ToContainerTypeConfig();

                if (config.GuaranteedDrops != null)
                    foreach (var drop in config.GuaranteedDrops)
                    {
                        if (drop.IsWeaponPreset || !IsAmmo(drop.DefinitionId)) continue;
                        Assert.Contains(drop.DefinitionId, usable.ToList(),
                            $"Container '{asset.name}' guarantees {drop.DefinitionId}, which no payload can chamber.");
                    }

                if (config.RandomPool != null)
                    foreach (var entry in config.RandomPool)
                    {
                        if (entry.IsCategory || !IsAmmo(entry.DefinitionId)) continue;
                        Assert.Contains(entry.DefinitionId, usable.ToList(),
                            $"Container '{asset.name}' can roll {entry.DefinitionId}, which no payload can chamber.");
                    }
            }
        }

        [Test]
        public void CraftRecipes_ProduceOnlyChamberableAmmo()
        {
            var usable = UsableAmmoIds();

            foreach (var recipe in CraftConstants.GetAll())
            {
                if (!IsAmmo(recipe.ResultItemId)) continue;
                Assert.Contains(recipe.ResultItemId, usable.ToList(),
                    $"Recipe '{recipe.RecipeId}' crafts {recipe.ResultItemId}, which no payload can chamber.");
            }
        }

        [Test]
        public void EveryAmmoDefinition_IsChamberable()
        {
            var usable = UsableAmmoIds();

            // The audit deleted every orphan caliber outright rather than parking it, so this
            // is the strong form: an ammo definition with no payload behind it is a bug, not a
            // placeholder. Parking one is not an option either — ItemBalance.DropWeightOf
            // falls back to a derived default > 0 for unlisted ids, so a definition with no
            // balance row silently rolls in loot.
            var orphans = ItemDefinition.Registry.Values
                .Where(d => d.Category == ItemCategory.Ammo && !usable.Contains(d.Id))
                .Select(d => d.Id)
                .ToList();

            Assert.IsEmpty(orphans,
                "Ammo no payload can chamber: " + string.Join(", ", orphans) +
                ". Add the payload that fires it, or drop the definition.");
        }

        [Test]
        public void EveryChamberableAmmo_HasAWayToRestock()
        {
            var usable = UsableAmmoIds();
            var restockable = new HashSet<string>();

            foreach (var shop in Resources.LoadAll<ShopDefinitionAsset>("Configs/Shops"))
                if (shop.Stock != null)
                    foreach (var entry in shop.Stock)
                        if (entry != null && !string.IsNullOrEmpty(entry.ItemDefId))
                            restockable.Add(entry.ItemDefId);

            foreach (var recipe in CraftConstants.GetAll())
                restockable.Add(recipe.ResultItemId);

            // A caliber the Builder can produce but the player can never buy or craft strands
            // that whole archetype after the free reserve from the build runs out.
            foreach (var ammoId in usable)
                Assert.IsTrue(restockable.Contains(ammoId),
                    $"{ammoId} is chamberable but has no shop stock and no craft recipe.");
        }
    }
}
