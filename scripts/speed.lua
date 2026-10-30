-- speed
local ws = game:GetService("Workspace")
local pl = game.Players.LocalPlayer
if pl and pl.Character and pl.Character:FindFirstChild("Humanoid") then
    pl.Character.Humanoid.WalkSpeed = 60
    print("walkspeed -> 60")
end
