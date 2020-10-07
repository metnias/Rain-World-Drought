using System;
using UnityEngine;
using MonoMod;
using RWCustom;

public class patch_MainLoopProcess : MainLoopProcess
{
    [MonoModIgnore]
    public patch_MainLoopProcess(ProcessManager manager, ProcessManager.ProcessID ID) : base(manager, ID)
    {
    }

    public extern void orig_RawUpdate(float dt);

    public void RawUpdate(float dt)
    {
        if((MainLoopProcess)this is RainWorldGame rwg)
        {
            if(rwg.Players.Count > 0)
            {
                patch_Player ply = rwg.Players[0].realizedCreature as patch_Player;
                if(ply != null)
                {
                    if (ply.PanicSlowdown > 0f)
                    {
                        framesPerSecond = Math.Min(framesPerSecond, Mathf.CeilToInt(40f * (1f - ply.PanicSlowdown)) * 32 + 8);
                        framesPerSecond = Math.Max(framesPerSecond, 8);
                    } else if(ply.Slowdown > 0f)
                    {
                        framesPerSecond = Math.Min(framesPerSecond, (int)Custom.LerpMap(ply.Slowdown, 1f, 0.8f, 40f, 15f));
                    }
                }
            }
        }
        orig_RawUpdate(dt);
    }
}