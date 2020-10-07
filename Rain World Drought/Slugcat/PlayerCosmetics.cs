using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class TailRing : PlayerCosmetics
{
    public TailRing(PlayerGraphics pGraphics, int startSprite, int tailSegment) : base(pGraphics, startSprite)
    {
        spritesOverlap = SpritesOverlap.InFront;
        this.tailSegment = tailSegment;
        numberOfSprites = 2;
    }



    public override void Update()
    {
    }

    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites[startSprite] = TriangleMesh.MakeLongMesh(1, false, true);
        sLeaser.sprites[startSprite + 1] = TriangleMesh.MakeLongMesh(1, false, true);
    }

    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

        patch_PlayerGraphics.PlayerTailData tailData = (pGraphics as patch_PlayerGraphics).TailPosition(tailSegment, timeStacker);

        (sLeaser.sprites[startSprite] as TriangleMesh).MoveVertice(0, tailData.pos - camPos);
        (sLeaser.sprites[startSprite] as TriangleMesh).MoveVertice(1, tailData.pos + tailData.dir - camPos);
        (sLeaser.sprites[startSprite] as TriangleMesh).MoveVertice(2, tailData.outerPos - camPos);
        (sLeaser.sprites[startSprite] as TriangleMesh).MoveVertice(3, tailData.outerPos + tailData.dir - camPos);

        (sLeaser.sprites[startSprite+1] as TriangleMesh).MoveVertice(0, tailData.pos - camPos);
        (sLeaser.sprites[startSprite+1] as TriangleMesh).MoveVertice(1, tailData.pos + tailData.dir - camPos);
        (sLeaser.sprites[startSprite+1] as TriangleMesh).MoveVertice(2, tailData.innerPos - camPos);
        (sLeaser.sprites[startSprite+1] as TriangleMesh).MoveVertice(3, tailData.innerPos + tailData.dir - camPos);

        if (rCam != null)
        {
            ApplyPalette(sLeaser, rCam, rCam.currentPalette);
        }
    }

    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        patch_Player ply = (patch_Player)pGraphics.owner;
        float voidInEffect = 0f;
        if ((pGraphics.owner as patch_Player).voidEnergy)
        {
            voidInEffect = ply.voidEnergyAmount / 1.2f;
        }
        Color color = Color.Lerp(PlayerGraphics.SlugcatColor(ply.playerState.slugcatCharacter), Color.white, voidInEffect);
        int order = -tailSegment;
        float alpha = 1f;
        if (ply.Slowdown == 0f)
        {
            alpha = ply.Energy * Mathf.Abs(Mathf.Sin(rCam.room.game.clock / 20.0375f + order) / 2f);
            alpha = alpha * 0.5f + 0.5f;
        }
        else if (ply.Slowdown > 0f)
        {
            alpha = 1f;
        }
        else
        {
            alpha = ply.Energy * Mathf.Abs(Mathf.Sin(rCam.room.game.clock / 40.075f)) / 1.2f;
        }

        //pGraphics.owner.room.world.rainCycle.timer;
        sLeaser.sprites[startSprite].alpha = alpha;
        sLeaser.sprites[startSprite + 1].alpha = alpha;

        sLeaser.sprites[startSprite].color = Color.Lerp(color, Color.white, alpha);//palette.blackColor;
        sLeaser.sprites[startSprite + 1].color = Color.Lerp(color, Color.white, alpha);//palette.blackColor;
        base.ApplyPalette(sLeaser, rCam, palette);
    }

    public int tailSegment;

}

// Two light sprites, one black one white
public class FocusSprites : PlayerCosmetics
{
    public FocusSprites(PlayerGraphics pGraphics, int startSprite) : base(pGraphics, startSprite)
    {
        spritesOverlap = SpritesOverlap.Behind;
        numberOfSprites = 2;
    }
    
    public override void Update()
    {
        lastFocus = focus;
        patch_Player ply = (patch_Player)pGraphics.owner;
        focus = Custom.LerpAndTick(focus, (ply.Focus + ply.Slowdown > 0f) ? 1f : 0f, 0.15f, 0.05f);
    }

    //public override string DefaultContainer => "Bloom";

    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        sLeaser.sprites[startSprite] = new FSprite("Futile_White")
        {
            shader = rCam.game.rainWorld.Shaders["FlatLight"]
        };
        sLeaser.sprites[startSprite + 1] = new FSprite("Futile_White")
        {
            shader = rCam.game.rainWorld.Shaders["FlatLight"]
        };
    }

    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

        Vector2 drawPos = Vector2.Lerp(
            Vector2.Lerp(pGraphics.drawPositions[0, 1], pGraphics.drawPositions[0, 0], timeStacker),
            Vector2.Lerp(pGraphics.drawPositions[1, 1], pGraphics.drawPositions[1, 0], timeStacker),
            0.5f);

        float alpha = Mathf.Lerp(lastFocus, focus, timeStacker);
        if (alpha == 0f)
        {
            sLeaser.sprites[startSprite + 0].isVisible = false;
            sLeaser.sprites[startSprite + 1].isVisible = false;
        }
        else
        {
            sLeaser.sprites[startSprite + 0].SetPosition(drawPos - camPos);
            sLeaser.sprites[startSprite + 1].SetPosition(drawPos - camPos);
            sLeaser.sprites[startSprite + 0].isVisible = true;
            sLeaser.sprites[startSprite + 1].isVisible = true;
            sLeaser.sprites[startSprite + 0].alpha = alpha * 0.3f;
            sLeaser.sprites[startSprite + 0].scale = 180f * alpha / 16f;
            sLeaser.sprites[startSprite + 1].alpha = alpha * 0.1f;
            sLeaser.sprites[startSprite + 1].scale = 80f * alpha / 16f;
        }
    }

    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        sLeaser.sprites[startSprite + 0].color = Color.black;
        sLeaser.sprites[startSprite + 1].color = Color.white;
        base.ApplyPalette(sLeaser, rCam, palette);
    }

    private bool greyscale;
    private float focus;
    private float lastFocus;
}


public abstract class PlayerCosmetics
{
    public PlayerCosmetics(PlayerGraphics pGraphics, int startSprite)
    {
        this.pGraphics = pGraphics;
        this.startSprite = startSprite;
    }

    public virtual void Update()
    {
    }

    public virtual void Reset()
    {
    }

    public virtual void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
    }

    public virtual void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
    }

    public virtual void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
        this.palette = palette;
    }

    public virtual string DefaultContainer => "Midground";

    public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
    {
        if(newContainer == null)
        {
            newContainer = rCam.ReturnFContainer(DefaultContainer);
        }
        for (int i = startSprite; i < startSprite + numberOfSprites; i++)
        {
            newContainer.AddChild(sLeaser.sprites[i]);
        }
    }

    public PlayerGraphics pGraphics;
    public int numberOfSprites;
    public int startSprite;
    public RoomPalette palette;
    public PlayerCosmetics.SpritesOverlap spritesOverlap;
    public enum SpritesOverlap
    {
        Behind,
        InFront
    }
}


