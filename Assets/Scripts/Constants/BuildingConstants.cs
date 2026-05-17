using System.Collections.Generic;
using State;

namespace Constants
{
    /// <summary>
    /// Single material requirement for one upgrade step.
    /// </summary>
    public readonly struct BuildingIngredient
    {
        public readonly string ItemId;
        public readonly int Count;

        public BuildingIngredient(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// Upgrade table for every <see cref="BuildingKind"/>. Buildings start at level 0
    /// and can be upgraded up to <see cref="MaxLevel"/>. Each entry in
    /// <see cref="UpgradeRecipes"/> defines the cost of advancing TO that level — index 0
    /// is the cost of going 0→1, index 4 is the cost of going 4→5. The array length is
    /// always equal to <see cref="MaxLevel"/>.
    ///
    /// Per-kind material flavor: workshop kinds lean on mechanical / electrical parts;
    /// med station leans on bio / chemical; quest / supply terminals lean on
    /// electronics + intel. Costs ramp roughly common → uncommon → rare.
    /// </summary>
    public static class BuildingConstants
    {
        public const int MaxLevel = 5;

        public static readonly IReadOnlyDictionary<BuildingKind, BuildingIngredient[][]> UpgradeRecipes
            = new Dictionary<BuildingKind, BuildingIngredient[][]>
            {
                [BuildingKind.Crafting] = new[]
                {
                    new[] { I("Pipes", 10),                   I("Metal_Parts", 15),         I("Duct_Tape", 5) },
                    new[] { I("Aluminum", 15),                I("Springs", 10),              I("Hydraulic_Seals", 3) },
                    new[] { I("Gear_Cluster", 5),             I("Rotary_Motor", 3),          I("Pneumatic_Valve", 3) },
                    new[] { I("Smart_Targeting_Unit", 2),     I("Adaptive_Circuit", 3),      I("Military_Components", 4) },
                    new[] { I("Nano_Filament", 3),            I("Magnetic_Alloy", 3),        I("Quantum_Relay", 1) },
                },

                [BuildingKind.WeaponBuilder] = new[]
                {
                    new[] { I("Mechanical_Parts", 10),        I("Gunpowder", 8),             I("Springs", 8) },
                    new[] { I("Gear_Cluster", 5),             I("Rotary_Motor", 3),          I("Camera_Optics", 2) },
                    new[] { I("Pulse_Emitter", 1),            I("Gyro_Stabilizer", 2),       I("Flux_Coil", 5) },
                    new[] { I("Smart_Targeting_Unit", 2),     I("Resonance_Plate", 3),       I("Crystal_Matrix", 2) },
                    new[] { I("Quantum_Relay", 1),            I("Phase_Shard", 1),           I("Magnetic_Alloy", 3) },
                },

                [BuildingKind.Stash] = new[]
                {
                    new[] { I("Aluminum", 15),                I("Pipes", 10),                I("Insulated_Wiring", 5) },
                    new[] { I("Structural_Foam", 10),         I("Metal_Parts", 20),          I("Mechanical_Parts", 10) },
                    new[] { I("Filtration_Membrane", 5),      I("Cooling_Fan", 3),           I("Sensor_Module", 3) },
                    new[] { I("Adaptive_Circuit", 3),         I("Pulse_Converter", 3),       I("Ion_Battery", 5) },
                    new[] { I("Crystal_Matrix", 2),           I("Synthetic_Quartz", 3),      I("Energy_Core", 1) },
                },

                [BuildingKind.SupplyTerminal] = new[]
                {
                    new[] { I("Electronics", 5),              I("Insulated_Wiring", 10),     I("Plastic", 8) },
                    new[] { I("Sensor_Module", 3),            I("Camera_Optics", 2),         I("Conductive_Gel", 5) },
                    new[] { I("Energy_Relay", 3),             I("Pulse_Converter", 3),       I("Ion_Battery", 5) },
                    new[] { I("Adaptive_Circuit", 3),         I("Smart_Targeting_Unit", 1),  I("Quantum_Relay", 1) },
                    new[] { I("Phase_Battery", 1),            I("Resonance_Plate", 2),       I("Military_Intel", 1) },
                },

                [BuildingKind.MedStation] = new[]
                {
                    new[] { I("Cloth", 10),                   I("Chemicals", 8),             I("Sterile_Wrap", 3) },
                    new[] { I("Filtration_Membrane", 5),      I("Bio_Compound", 5),          I("Chemical_Catalyst", 5) },
                    new[] { I("Bio_Foam", 3),                 I("Sterile_Wrap", 8),          I("Conductive_Gel", 3) },
                    new[] { I("Neural_Gel", 2),               I("Bio_Sample_Case", 1),       I("Pulse_Emitter", 1) },
                    new[] { I("Crystal_Matrix", 1),           I("Phase_Battery", 1),         I("Bio_Sample_Case", 2) },
                },

                [BuildingKind.QuestTerminal] = new[]
                {
                    new[] { I("Electronics", 5),              I("Camera_Optics", 2),         I("Insulated_Wiring", 5) },
                    new[] { I("Sensor_Module", 3),            I("Motion_Sensor", 3),         I("Ion_Battery", 3) },
                    new[] { I("Energy_Relay", 3),             I("Conductive_Gel", 5),        I("Smart_Targeting_Unit", 1) },
                    new[] { I("Quantum_Relay", 1),            I("Adaptive_Circuit", 3),      I("Military_Intel", 2) },
                    new[] { I("Phase_Shard", 1),              I("Crystal_Matrix", 2),        I("Military_Intel", 3) },
                },
            };

        /// <summary>
        /// Returns the recipe for the upgrade FROM the given current level. Null when
        /// already at <see cref="MaxLevel"/> or when the kind has no recipe table yet.
        /// </summary>
        public static BuildingIngredient[] GetUpgradeRecipe(BuildingKind kind, int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= MaxLevel) return null;
            if (!UpgradeRecipes.TryGetValue(kind, out var table)) return null;
            if (currentLevel >= table.Length) return null;
            return table[currentLevel];
        }

        static BuildingIngredient I(string id, int count) => new BuildingIngredient(id, count);
    }
}
