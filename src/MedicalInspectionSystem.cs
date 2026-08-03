using System.Text;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class MedicalInspectionSystem
    {
        private static readonly string[] BleedNames =
        {
            "No Bleeding",
            "Minor Bleeding",
            "Moderate Bleeding",
            "Severe Bleeding",
            "Arterial Bleeding"
        };

        private static readonly string[] ShockNames =
        {
            "No Shock",
            "Mild Shock",
            "Moderate Shock",
            "Severe Shock",
            "Critical Shock"
        };

        private static readonly string[] PartNames =
        {
            "Head",
            "Torso",
            "Left Arm",
            "Right Arm",
            "Left Leg",
            "Right Leg"
        };

        private static readonly string[] FractureNames =
        {
            "No Fracture",
            "Sprain",
            "Fracture",
            "Critical Fracture"
        };

        public static bool IsArm(BodyPart part)
        {
            return part == BodyPart.LeftArm || part == BodyPart.RightArm;
        }

        public static bool IsLeg(BodyPart part)
        {
            return part == BodyPart.LeftLeg || part == BodyPart.RightLeg;
        }

        public static bool IsLimb(BodyPart part)
        {
            return IsArm(part) || IsLeg(part);
        }

        public static string GetBleedingName(BleedSeverity severity)
        {
            int index = (int)severity;
            return index >= 0 && index < BleedNames.Length ? BleedNames[index] : BleedNames[0];
        }

        public static string GetShockName(ShockSeverity severity)
        {
            int index = (int)severity;
            return index >= 0 && index < ShockNames.Length ? ShockNames[index] : ShockNames[0];
        }

        public static string BuildBodyPartInspection(HealthManager manager, BodyPart part)
        {
            StringBuilder builder = new StringBuilder(192);
            builder.Append(GetPartLabel(part));
            builder.Append('\n');

            LimbHealth limb = manager.GetLimb(part);
            bool any = false;
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            if (bleed != BleedSeverity.None)
            {
                builder.Append(GetBleedingName(bleed));
                builder.Append('\n');
                any = true;
            }

            if (limb.Fracture != FractureState.None)
            {
                builder.Append(GetFractureName(limb.Fracture));
                builder.Append('\n');
                any = true;
            }

            if (part == BodyPart.Head && manager.Brain.HasActiveConcussion)
            {
                builder.Append(manager.Brain.Concussion >= 0.55f ? "Severe Concussion\n" : "Concussion\n");
                any = true;
            }

            if (part == BodyPart.Torso)
            {
                if (manager.Lungs.HasCollapsedLung || manager.Lungs.OxygenNormalized < 0.72f)
                {
                    builder.Append("Lung Trauma\n");
                    any = true;
                }

                if (manager.Bones.FracturedRibCount > 0)
                {
                    builder.Append("Rib Fracture x");
                    builder.Append(manager.Bones.FracturedRibCount);
                    builder.Append('\n');
                    any = true;
                }
            }

            if (!any)
                builder.Append("Stable\n");

            builder.Append("\nRecommended Treatment:\n");
            AppendTreatmentForPart(builder, manager, part);
            return builder.ToString();
        }

        public static string BuildNeckInspection(HealthManager manager)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("Neck\n");
            builder.Append(NeckTraumaSystem.GetDisplayState(manager.Neck.State));
            builder.Append("\n\nRecommended Treatment:\n");
            if (manager.Neck.State == NeckInjuryState.Healthy)
                builder.Append("No treatment required\n");
            else
            {
                if (manager.Pain >= 45f)
                    builder.Append("Morphine\n");
                builder.Append("Medkit\nRest\n");
            }

            return builder.ToString();
        }

        public static string BuildMedicationInspection(HealthManager manager)
        {
            if (manager == null)
                return string.Empty;

            if (manager.Medication.OverdoseRisk <= 0.35f && !manager.Medication.CrashActive && !manager.Rehabilitation.IsRecovering)
                return "Medication / Recovery\n  Stable\n";

            StringBuilder builder = new StringBuilder(160);
            builder.Append("Medication / Recovery\n");
            if (manager.Medication.OverdoseRisk > 0.35f)
            {
                builder.Append("  Overdose Risk: ");
                builder.Append(Mathf.RoundToInt(manager.Medication.OverdoseRisk * 100f));
                builder.Append("%\n");
            }
            if (manager.Medication.RespiratoryDepression > 0.20f)
                builder.Append("  Slow breathing\n");
            if (manager.Medication.CrashActive)
                builder.Append("  Stimulant crash\n");
            if (manager.Rehabilitation.IsRecovering)
                builder.Append("  Rehabilitation weakness\n");
            builder.Append("  Treatment: monitor breathing, rest\n");
            return builder.ToString();
        }

        public static void AppendTreatmentForPart(StringBuilder builder, HealthManager manager, BodyPart part)
        {
            int start = builder.Length;
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            AppendBleedingTreatment(builder, bleed, part);

            LimbHealth limb = manager.GetLimb(part);
            if (IsLimb(part) && limb.Fracture != FractureState.None)
                builder.Append("Splint\n");
            if (manager.Pain >= 55f)
                builder.Append("Morphine\n");
            else if (manager.Pain >= 28f)
                builder.Append("Painkillers\n");
            if (manager.Shock.Severity >= ShockSeverity.Moderate || manager.Bleeding.BloodVolumeMl < Config.UnconsciousBloodMl)
                builder.Append("Blood Pack\n");
            if (manager.Consciousness.State == ConsciousnessState.Unconscious || manager.Coma.IsActive)
                builder.Append("Adrenaline if stable\n");

            if (builder.Length == start)
                builder.Append("No treatment required\n");
        }

        public static string GetPartLabel(BodyPart part)
        {
            int index = (int)part;
            return index >= 0 && index < PartNames.Length ? PartNames[index] : "Body";
        }

        private static string GetFractureName(FractureState state)
        {
            int index = (int)state;
            return index >= 0 && index < FractureNames.Length ? FractureNames[index] : FractureNames[0];
        }

        private static void AppendBleedingTreatment(StringBuilder builder, BleedSeverity bleed, BodyPart part)
        {
            if (bleed == BleedSeverity.None)
                return;

            bool limbBleed = IsLimb(part);
            if (bleed == BleedSeverity.Arterial || (bleed == BleedSeverity.Severe && limbBleed))
            {
                builder.Append("Tourniquet\nBandage\nBlood Pack\n");
                return;
            }

            if (bleed == BleedSeverity.Severe)
            {
                builder.Append("Bandage\nBlood Pack\n");
                return;
            }

            builder.Append(bleed == BleedSeverity.Medium ? "Bandage Recommended\n" : "Bandage\n");
        }
    }
}
