-- ZBoneCity Lua bridge entry point.
-- This file is meant to run as a LuaMod LuaBehaviour script. The C# mod sends
-- compact medical snapshots and event impulses through LuaMod's event bus.

ZBC = ZBC or {}
ZBC.Bridge = ZBC.Bridge or {}

local function clamp(value, minValue, maxValue)
    if value < minValue then return minValue end
    if value > maxValue then return maxValue end
    return value
end

local function ensure_state()
    ZBC.State = ZBC.State or {
        blood = 1.0,
        pain = 0.0,
        shock = 0.0,
        oxygen = 1.0,
        consciousness = 1.0,
        pulse = 70.0,
        bleedingRate = 0.0,
        internalBleeding = 0.0,
        infectionRisk = 0.0,
        fractures = 0,
        unconscious = false,
        dead = false,
        lastDamageTime = -1000.0,
        lastTreatmentTime = -1000.0
    }

    return ZBC.State
end

local function now()
    if Time ~= nil then return Time.time end
    return os.clock()
end

function ZBC.Bridge.OnMedicalSnapshot(
    blood,
    pain,
    shock,
    oxygen,
    consciousness,
    pulse,
    bleedingRate,
    internalBleeding,
    infectionRisk,
    fractures,
    unconscious,
    dead)

    local state = ensure_state()
    state.blood = clamp(tonumber(blood) or state.blood, 0.0, 1.0)
    state.pain = clamp(tonumber(pain) or state.pain, 0.0, 1.5)
    state.shock = clamp(tonumber(shock) or state.shock, 0.0, 1.5)
    state.oxygen = clamp(tonumber(oxygen) or state.oxygen, 0.0, 1.0)
    state.consciousness = clamp(tonumber(consciousness) or state.consciousness, 0.0, 1.0)
    state.pulse = clamp(tonumber(pulse) or state.pulse, 0.0, 220.0)
    state.bleedingRate = math.max(0.0, tonumber(bleedingRate) or 0.0)
    state.internalBleeding = clamp(tonumber(internalBleeding) or 0.0, 0.0, 1.0)
    state.infectionRisk = clamp(tonumber(infectionRisk) or 0.0, 0.0, 1.0)
    state.fractures = math.max(0, tonumber(fractures) or 0)
    state.unconscious = unconscious == true
    state.dead = dead == true

    if ZBC.Medical ~= nil and ZBC.Medical.UpdateFromSnapshot ~= nil then
        ZBC.Medical.UpdateFromSnapshot(state)
    end
end

function ZBC.Bridge.OnDamageProcessed(part, damageType, damage, pain, bleedFactor)
    local state = ensure_state()
    state.lastDamageTime = now()

    if ZBC.Medical ~= nil and ZBC.Medical.ApplyDamageImpulse ~= nil then
        ZBC.Medical.ApplyDamageImpulse(tostring(part), tostring(damageType), tonumber(damage) or 0.0, tonumber(pain) or 0.0, tonumber(bleedFactor) or 0.0)
    end
end

function ZBC.Bridge.OnTreatmentApplied(itemType, part)
    local state = ensure_state()
    state.lastTreatmentTime = now()

    if ZBC.Medical ~= nil and ZBC.Medical.ApplyTreatment ~= nil then
        ZBC.Medical.ApplyTreatment(tostring(itemType), tostring(part))
    end
end

function Start()
    ensure_state()

    if API_Events ~= nil then
        API_Events.BL_SubscribeEvent("ZBC_MedicalSnapshot", BL_This, "ZBC_Bridge_OnMedicalSnapshot")
        API_Events.BL_SubscribeEvent("ZBC_DamageProcessed", BL_This, "ZBC_Bridge_OnDamageProcessed")
        API_Events.BL_SubscribeEvent("ZBC_TreatmentApplied", BL_This, "ZBC_Bridge_OnTreatmentApplied")
    end
end

function ZBC_Bridge_OnMedicalSnapshot(...)
    ZBC.Bridge.OnMedicalSnapshot(...)
end

function ZBC_Bridge_OnDamageProcessed(...)
    ZBC.Bridge.OnDamageProcessed(...)
end

function ZBC_Bridge_OnTreatmentApplied(...)
    ZBC.Bridge.OnTreatmentApplied(...)
end
