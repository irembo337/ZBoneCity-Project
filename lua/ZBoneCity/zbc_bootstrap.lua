-- Attach this script to a LuaBehaviour when using BonelabMeridian LuaMod.
-- It loads the ZBoneCity Lua gameplay layer and listens to C# bridge events.

local base = "ZBoneCity/"

require(base .. "Core/zbc_core")
require(base .. "Blood/bleeding")
require(base .. "Pain/pain")
require(base .. "Shock/shock")
require(base .. "Fractures/fractures")
require(base .. "Inventory/medical_items")
require(base .. "Containers/container_defaults")
require(base .. "HUD/hud_model")
require(base .. "Medical/organism")
require(base .. "Bridge/zbc_bridge")

function SlowUpdate()
    if ZBC ~= nil and ZBC.Medical ~= nil and ZBC.Medical.Tick ~= nil then
        ZBC.Medical.Tick(0.2)
    end
end
