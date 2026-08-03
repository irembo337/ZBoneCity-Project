ZBC = ZBC or {}
ZBC.Inventory = ZBC.Inventory or {}

ZBC.Inventory.Items = {
    Bandage = { useTime = 2.0, stackSize = 4, role = "minor_bleeding" },
    Tourniquet = { useTime = 2.4, stackSize = 2, role = "arterial_bleeding" },
    Splint = { useTime = 4.5, stackSize = 2, role = "fracture" },
    Morphine = { useTime = 1.2, stackSize = 1, role = "analgesic" },
    Adrenaline = { useTime = 1.0, stackSize = 1, role = "stimulant" },
    ETGStimulator = { useTime = 1.0, stackSize = 1, role = "advanced_stimulant" },
    SJ1Stimulator = { useTime = 1.0, stackSize = 1, role = "combat_stimulant" },
    Painkillers = { useTime = 1.8, stackSize = 3, role = "pain" },
    BloodPack = { useTime = 6.0, stackSize = 1, role = "blood_restore" },
    Medkit = { useTime = 4.0, stackSize = 1, role = "general_care" }
}
