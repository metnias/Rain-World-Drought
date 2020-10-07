using System;
using RWCustom;
using MonoMod;
using UnityEngine;
using Noise;
using System.Collections.Generic;
using Random = UnityEngine.Random;


[MonoModPatch("global::Player")]
class patch_Player : Player
{
    private const int maxEnergy = 10;
    private const int focusDuration = 60;
    private const int slowdownDuration = 40;
    private const float parryRadius = 150f;
    private const int maxExtraJumps = 2;
    private const float jumpForce = 7.5f;
    private int jumpsSinceGrounded = 0;
    private int energy; // Ability uses left before food is used, replenishes when focus is used at 0 with a non-empty food bar
    private int focusLeft;
    private int slowdownLeft;
    private bool panicSlowdown;
    private float ticksUntilPanicHit;
    private bool canTripleJump;
    private int noFocusJumpCounter;
    public bool jumpQueued;

    public float Focus => focusLeft / (float)focusDuration;
    public float Slowdown => slowdownLeft / (float)slowdownDuration;
    public float PanicSlowdown => (slowdownLeft == 0 || !panicSlowdown) ? 0f : Custom.LerpMap(ticksUntilPanicHit, 20f, 5f, 0f, Mathf.Clamp01((1f - Slowdown) * 40f));
    public float Energy {
        get => energy / (float)maxEnergy;
        set => energy = (int)Mathf.InverseLerp(0, maxEnergy, value);
    }

    //Ending code
    public bool voidEnergy = false; //true if the void effects are controlling the maxEnergy
    public float voidEnergyAmount = 0f;
    public bool past22000 = false; //true if the player is in the void past -22000 y
    public bool past25000 = false; //true if the player is in the void past -25000 y
    //-------------------
    bool hibernation1 = false;
    bool hibernation2 = false;
    public WalkerBeast.PlayerInAntlers playerInAnt;
    public Player.AnimationIndex lastAnimation;

    [MonoModIgnore]
    patch_Player(AbstractCreature abstractCreature, World world) : base(abstractCreature, world) { }

    public extern void orig_ctor(AbstractCreature abstractCreature, World world);

    [MonoModConstructor]
    public void ctor(AbstractCreature abstractCreature, World world)
    {
        orig_ctor(abstractCreature, world);
        this.lastAnimation = Player.AnimationIndex.None;
        hibernation1 = false;
        hibernation2 = false;
        this.pearlConversation = new PearlConversation(this);
        voidEnergy = false;

        energy = maxEnergy;
    }

    public void SwallowObject(int grasp)
    {
        if (grasp < 0 || base.grasps[grasp] == null)
        {
            return;
        }
        if (this.room.game.session is StoryGameSession && (this.room.game.session as StoryGameSession).saveState.miscWorldSaveData.moonRevived)
        {
            this.pearlConversation.PlayerSwallowItem(base.grasps[grasp].grabbed);
        }
        this.objectInStomach = base.grasps[grasp].grabbed.abstractPhysicalObject;
        this.ReleaseGrasp(grasp);
        this.objectInStomach.realizedObject.RemoveFromRoom();
        this.objectInStomach.Abstractize(base.abstractCreature.pos);
        this.objectInStomach.Room.RemoveEntity(this.objectInStomach);
        BodyChunk mainBodyChunk = base.mainBodyChunk;
        mainBodyChunk.vel.y = mainBodyChunk.vel.y + 2f;
        this.room.PlaySound(SoundID.Slugcat_Swallow_Item, base.mainBodyChunk);
    }

    public extern void orig_MovementUpdate(bool eu);

    public bool IsGrounded(bool feetMustBeGrounded)
    {
        if (canJump > 0 && canWallJump == 0) return true;
        if (animation == AnimationIndex.SurfaceSwim) return true;
        if (bodyMode == BodyModeIndex.ClimbingOnBeam) return true;
        if (feetMustBeGrounded) return false;
        if (canWallJump > 0) return true;
        return false;
    }

    public void MovementUpdate(bool eu)
    {
        //if (Input.GetKeyDown(KeyCode.C))
        //{
        //    patch_AbstractCreature abstractCreature = new patch_AbstractCreature(this.room.world, (patch_CreatureTemplate)StaticWorld.GetCreatureTemplate((CreatureTemplate.Type)patch_CreatureTemplate.Type.SeaDrake), null, base.abstractCreature.pos, this.room.game.GetNewID());
        //    abstractCreature.Room.AddEntity(abstractCreature);
        //    abstractCreature.RealizeInRoom();
        //    abstractCreature.ChangeRooms(base.abstractCreature.pos);
        //    return;
        //}
        //else if (Input.GetKeyDown(KeyCode.F))
        //{
        //    patch_AbstractCreature abstractCreature = new patch_AbstractCreature(this.room.world, (patch_CreatureTemplate)StaticWorld.GetCreatureTemplate((CreatureTemplate.Type)patch_CreatureTemplate.Type.WalkerBeast), null, base.abstractCreature.pos, this.room.game.GetNewID());
        //    abstractCreature.Room.AddEntity(abstractCreature);
        //    abstractCreature.RealizeInRoom();
        //    abstractCreature.ChangeRooms(base.abstractCreature.pos);
        //    return;
        //}
        //else if (Input.GetKeyDown(KeyCode.D))
        //{
        //    //patch_AbstractCreature abstractCreature = new patch_AbstractCreature(this.room.world, (patch_CreatureTemplate)StaticWorld.GetCreatureTemplate((CreatureTemplate.Type)patch_CreatureTemplate.Type.Wolf), null, base.abstractCreature.pos, this.room.game.GetNewID());
        //    //abstractCreature.Room.AddEntity(abstractCreature);
        //    //abstractCreature.RealizeInRoom();
        //    //abstractCreature.ChangeRooms(base.abstractCreature.pos);
        //    //return;
        //}

        bool focusJumped = false;
        if (noFocusJumpCounter > 0)
            noFocusJumpCounter--;
        else if ((slowdownLeft > 0 || canTripleJump) && !IsGrounded(false))
        {
            if ((input[0].jmp || jumpQueued) && !input[1].jmp)
            {
                FocusJump();
                focusJumped = true;
                jumpQueued = false;
            }
        }

        if ((animation == AnimationIndex.BeamTip) || (animation == AnimationIndex.StandOnBeam))
            noFocusJumpCounter = 2;
        
        orig_MovementUpdate(eu);

        if (focusJumped)
        {
            wantToJump = 0;
        }

        if (animation == AnimationIndex.DeepSwim && lastAnimation != AnimationIndex.DeepSwim)
        {
            this.room.InGameNoise(new InGameNoise(base.bodyChunks[1].pos, 350f, this, 2f));
        }
        else if (animation == AnimationIndex.SurfaceSwim && lastAnimation != AnimationIndex.SurfaceSwim)
        {
            this.room.InGameNoise(new InGameNoise(base.bodyChunks[1].pos, 350f, this, 2f));
        }
        lastAnimation = this.animation;

        DoAbilities();
    }

    // Activate focus state by tapping map
    // While in focus state, leaving the ground (or starting off of the ground) slows down time
    // Tapping jump while slowed activates doublejump
    // If a deadly projectile get close to you, time slows as well
    // Tapping jump during this slow parries and doublejumps
    // If the deadly projectile is your own, it increases its speed and damage
    private int mapHeld;
    private void DoAbilities()
    {
        if (input[0].mp) mapHeld++;
        else
        {
            if (input[1].mp && mapHeld < 10 && IsGrounded(true))
                EnterFocus();
            mapHeld = 0;
        }

        if (focusLeft > 0)
        {
            bool fromWeapon = false;
            bool enterSlowdown = false;
            // Enter slowdown when the player leaves the ground
            if (bodyChunks[0].ContactPoint.y != -1 && bodyChunks[1].ContactPoint.y != -1 && !IsGrounded(false))
            {
                Debug.Log("Entered slowdown from jump");
                enterSlowdown = true;
            }

            // Enter slowdown if a deadly weapon is projected to hit the player
            if (!enterSlowdown)
            {
                if(PanicWeapon(out float ticksUntilContact) != null) {
                    Debug.Log("Entered slowdown from threat (" + ticksUntilContact + " ticks until contact)");
                    enterSlowdown = true;
                    fromWeapon = true;
                }
            }

            // Should enter slowdown if non-weapon deadly projectiles (king tusks) are near
            if (enterSlowdown)
                EnterSlowdown(fromWeapon);
        }

        if(slowdownLeft > 0 && !panicSlowdown)
        {
            if (PanicWeapon(out float ticksUntilContact) != null)
            {
                Debug.Log("Switch to panic slowdown from threat (" + ticksUntilContact + " ticks until contact)");
                panicSlowdown = true;
                slowdownLeft = Math.Min(slowdownLeft, slowdownDuration - 10);
            }
        } else if(panicSlowdown)
        {
            if (PanicWeapon(out float ticksUntilContact) != null)
                ticksUntilPanicHit = ticksUntilContact;
            else
            {
                panicSlowdown = false;
                ticksUntilPanicHit = 0;
            }
        }
    }

    private Weapon PanicWeapon(out float ticksUntilContact)
    {
        Weapon closestWeapon = FindClosestWeapon(out ticksUntilContact);

        if (closestWeapon != null)
        {
            bool canSeeWeapon = false;
            for (int i = 0; i < bodyChunks.Length; i++)
                if (room.VisualContact(closestWeapon.firstChunk.pos, bodyChunks[i].pos))
                {
                    canSeeWeapon = true;
                    break;
                }
            if (ticksUntilContact < 10 || (ticksUntilContact < 20 && canSeeWeapon))
                return closestWeapon;
        }

        return null;
    }

    private Weapon FindClosestWeapon(out float ticksUntilContact)
    {
        float minTicksUntilContact = float.PositiveInfinity;
        Weapon closestWeapon = null;
        for (int layer = room.physicalObjects.Length - 1; layer >= 0; layer--)
        {
            List<PhysicalObject> objs = room.physicalObjects[layer];
            for (int i = objs.Count - 1; i >= 0; i--)
            {
                if (objs[i] is Weapon wep && wep.mode == Weapon.Mode.Thrown)
                {
                    if (!IsWeaponDeadly(wep)) continue;

                    if (wep.thrownBy == this) continue;
                    for (int chunk = bodyChunks.Length - 1; chunk >= 0; chunk--)
                    {
                        float ticks = (bodyChunks[chunk].pos.x - wep.firstChunk.pos.x) / wep.firstChunk.vel.x;
                        if (ticks > 0f && ticks < minTicksUntilContact)
                        {
                            if (Mathf.Abs(PredictWeaponHeight(wep.firstChunk.pos, wep.firstChunk.vel, bodyChunks[chunk].pos.x, wep.gravity) - bodyChunks[chunk].pos.y) < bodyChunks[chunk].rad * 4f)
                            {
                                minTicksUntilContact = ticks;
                                closestWeapon = wep;
                            }
                        }
                    }
                }
            }
        }

        ticksUntilContact = minTicksUntilContact;
        return closestWeapon;
    }

    private static bool IsWeaponDeadly(Weapon wep)
    {
        if (!wep.HeavyWeapon) return false;
        if (wep is Rock) return false;
        return true;
    }

    private static float PredictWeaponHeight(Vector2 p, Vector2 v, float targetX, float gravity)
    {
        float dx = targetX - p.x;
        return v.y / v.x * dx - gravity / 2f / Mathf.Abs(Mathf.Pow(v.x, 3f)) * dx * dx + p.y;
    }

    private void EnterFocus()
    {
        Debug.Log("Entered focus");
        focusLeft = focusDuration;
    }

    private void EnterSlowdown(bool panic)
    {
        Debug.Log($"Entered slowdown (panic: {panic})");
        panicSlowdown = panic;
        ticksUntilPanicHit = int.MaxValue / 2;
        focusLeft = 0;
        slowdownLeft = slowdownDuration - (panic ? 10 : 0);
    }

    private void FocusJump()
    {
        wantToJump = 0;
        canJump = 0;
        room.PlaySound(SoundID.Vulture_Feather_Hit_Terrain, mainBodyChunk, false, 3.25f, 0.8f + 0.05f * jumpsSinceGrounded);
        room.PlaySound(SoundID.Shelter_Little_Hatch_Open, mainBodyChunk, false, 3.25f, 1.2f + 0.05f * jumpsSinceGrounded);
        room.InGameNoise(new InGameNoise(bodyChunks[1].pos, 350f, this, 1f));

        canTripleJump = jumpsSinceGrounded < maxExtraJumps;
        float strength = Custom.LerpMap(jumpsSinceGrounded, 0, maxExtraJumps, 2f, 1f);
        jumpsSinceGrounded++;
        Vector2 jumpDir = input[0].IntVec.ToVector2();
        if(input[0].gamePad)
        {
            if (!room.game.rainWorld.setup.devToolsActive || (input[0].analogueDir.sqrMagnitude != 0f))
            {
                jumpDir = input[0].analogueDir;
                if (Mathf.Abs(jumpDir.x) > 0.9f) jumpDir.Set(Mathf.Sign(jumpDir.x), 0f);
                else if (Mathf.Abs(jumpDir.y) > 0.9f) jumpDir.Set(0f, Mathf.Sign(jumpDir.y));
                jumpDir.Normalize();
            }
        }
        jumpDir.y += 0.25f;

        if (jumpDir.x == 0 && jumpDir.y == 0) jumpDir.y = 1;

        // Correct jump power on diagonals
        jumpDir.Normalize();

        Vector2 force = new Vector2(jumpDir.x * strength, jumpDir.y * strength);
        BoostChunk(bodyChunks[0], jumpForce * force);
        BoostChunk(bodyChunks[1], jumpForce * force * (5.5f / 7.5f));

        // Create a ring effect
        room.AddObject(new JumpPulse(bodyChunks[0].pos * 0.5f + bodyChunks[1].pos * 0.5f - force.normalized * 10f, -force));

        // Parry weapons
        bool parried = false;
        Vector2 parryCenter = Vector2.Lerp(bodyChunks[0].pos, bodyChunks[1].pos, 0.5f);
        for(int i = room.updateList.Count - 1; i >= 0; i--)
        {
            if(room.updateList[i] is Weapon wep)
            {
                if(IsWeaponDeadly(wep) && (wep.mode == Weapon.Mode.Thrown))
                {
                    Vector2 delta = wep.firstChunk.pos - parryCenter;
                    if (Vector2.SqrMagnitude(delta) < parryRadius * parryRadius)
                    {
                        parried = true;
                        if (wep.thrownBy == this)
                        {
                            // Boost spears if thrown by yourself
                            if(Mathf.Sign(wep.firstChunk.vel.x) == Mathf.Sign(wep.firstChunk.pos.x - parryCenter.x))
                            {
                                wep.firstChunk.vel *= 1.5f;
                                if (wep is Spear spr)
                                {
                                    spr.spearDamageBonus += 0.5f;
                                    for (int j = Random.Range(2, 5); j >= 0; j--)
                                        room.AddObject(new MouseSpark(spr.firstChunk.pos, (Random.insideUnitCircle - spr.firstChunk.vel.normalized * 2f) * 5f, Random.value * 0.25f + 0.25f, Color.white));
                                }
                            }
                        }
                        else
                        {

                            for (int j = Random.Range(2, 5); j >= 0; j--)
                                room.AddObject(new MouseSpark(wep.firstChunk.pos, (Random.insideUnitCircle + delta.normalized) * 5f, Random.value * 0.25f + 0.25f, Color.white));
                            wep.firstChunk.vel *= -0.25f;
                            wep.ChangeMode(Weapon.Mode.Free);
                        }
                    }
                }
            }
        }
        if (parried)
            Click();

        focusLeft = 0;
        slowdownLeft = 0;
    }

    private void BoostChunk(BodyChunk chunk, Vector2 vel)
    {
        // Set velocity, keeping the component of motion facing the new direction
        Vector2 dir = vel.normalized;
        dir.Set(dir.y, -dir.x);
        Vector2 oldVel = chunk.vel;
        oldVel -= dir * Vector2.Dot(oldVel, dir);
        if (Vector2.Dot(oldVel, vel) < 0f) oldVel.Set(0f, 0f);
        chunk.vel = vel + oldVel * 0.5f;
    }

    public extern void orig_Update(bool eu);
    
    public void Update(bool eu)
    {
        orig_Update(eu);

        if (focusLeft > 0)
        {
            focusLeft--;
            if (focusLeft == 0) Debug.Log("Exited focus from timer");
        }
        if (slowdownLeft > 0)
        {
            slowdownLeft--;
            if (slowdownLeft == 0) Debug.Log("Exited slowdown from timer");
        }

        if (IsGrounded(true))
        {
            jumpsSinceGrounded = 0;
            canTripleJump = false;
        }

        if (this.Consious && !Malnourished && this.room != null && this.room.game != null && this.room.game.IsStorySession)
        {
            this.pearlConversation.Update(eu);
        }

        jumpQueued = false;
    }

    public override Color ShortCutColor()
    {
        if ((State as PlayerState).slugcatCharacter == 1)
        {
            return new Color(0.4f, 0.49411764705f, 0.8f);
        }
        return PlayerGraphics.SlugcatColor((State as PlayerState).slugcatCharacter);
    }

    public void Click()
    {
        if (bodyChunks[1].submersion == 1f)
        {
            room.AddObject(new ShockWave(bodyChunks[0].pos, 160f * Mathf.Lerp(0.65f, 1.5f, UnityEngine.Random.value), 0.07f, 9));
        }
        else
        {
            room.AddObject(new ShockWave(bodyChunks[0].pos, 100f * Mathf.Lerp(0.65f, 1.5f, UnityEngine.Random.value), 0.07f, 6));
            for (int i = 0; i < 10; i++)
            {
                room.AddObject(new WaterDrip(bodyChunks[0].pos, Custom.DegToVec(UnityEngine.Random.value * 360f) * Mathf.Lerp(4f, 21f, UnityEngine.Random.value), false));
            }
        }
        room.PlaySound(SoundID.Lizard_Head_Shield_Deflect, mainBodyChunk, false, 2f, 1f);
    }
    
    public extern void orig_Grabbed(Creature.Grasp grasp);

    public override void Grabbed(Creature.Grasp grasp)
    {
        orig_Grabbed(grasp);
        if (grasp.grabber is Lizard || grasp.grabber is Vulture || grasp.grabber is BigSpider || grasp.grabber is DropBug || grasp.grabber is SeaDrake)
        {
            this.dangerGraspTime = 0;
            this.dangerGrasp = grasp;
        }
    }

    private void LungUpdate()
    {
        this.airInLungs = Mathf.Min(this.airInLungs, 1f - this.rainDeath);
        if (base.firstChunk.submersion > 0.9f && !this.room.game.setupValues.invincibility)
        {
            if (!this.submerged)
            {
                this.swimForce = Mathf.InverseLerp(0f, 8f, Mathf.Abs(base.firstChunk.vel.x));
                this.swimCycle = 0f;
            }
            float num = this.airInLungs;
            if (this.room.game.IsStorySession)
            {
                
                this.airInLungs -= 1f / ((((this.room.game.session as StoryGameSession).saveState.miscWorldSaveData as patch_MiscWorldSaveData).isImproved ? 3f : 1f) * 40f * ((!this.lungsExhausted) ? 9f : 4.5f) * ((this.input[0].y != 1 || this.input[0].x != 0 || this.airInLungs >= 0.333333343f) ? 1f : 1.5f) * ((float)this.room.game.setupValues.lungs / 100f)) * this.slugcatStats.lungsFac;
            }
            else
            {
                this.airInLungs -= 1f / (40f * ((!this.lungsExhausted) ? 9f : 4.5f) * ((this.input[0].y != 1 || this.input[0].x != 0 || this.airInLungs >= 0.333333343f) ? 1f : 1.5f) * ((float)this.room.game.setupValues.lungs / 100f)) * this.slugcatStats.lungsFac;
            }
            if (this.airInLungs < 0.6666667f && num >= 0.6666667f)
            {
                this.room.AddObject(new Bubble(base.firstChunk.pos, base.firstChunk.vel, false, false));
            }
            bool flag = this.airInLungs <= 0f && this.input[0].y == 1 && this.room.FloatWaterLevel(base.mainBodyChunk.pos.x) - base.mainBodyChunk.pos.y < 200f;
            if (flag)
            {
                for (int i = this.room.GetTilePosition(base.mainBodyChunk.pos).y; i <= this.room.defaultWaterLevel; i++)
                {
                    if (this.room.GetTile(this.room.GetTilePosition(base.mainBodyChunk.pos).x, i).Solid)
                    {
                        flag = false;
                        break;
                    }
                }
            }
            if (this.airInLungs <= ((!flag) ? 0f : -0.3f) && base.mainBodyChunk.submersion == 1f && base.bodyChunks[1].submersion > 0.5f)
            {
                this.airInLungs = 0f;
                this.Stun(10);
                this.drown += 0.008333334f;
                if (this.drown >= 1f)
                {
                    this.Die();
                }
            }
            else if (this.airInLungs < 0.333333343f)
            {
                if (this.slowMovementStun < 1)
                {
                    this.slowMovementStun = 1;
                }
                if (UnityEngine.Random.value < 0.5f)
                {
                    base.firstChunk.vel += Custom.DegToVec(UnityEngine.Random.value * 360f) * UnityEngine.Random.value;
                }
                if (this.input[0].y < 1)
                {
                    base.bodyChunks[1].vel *= Mathf.Lerp(1f, 0.9f, Mathf.InverseLerp(0f, 0.333333343f, this.airInLungs));
                }
                if ((UnityEngine.Random.value > this.airInLungs * 2f || this.lungsExhausted) && UnityEngine.Random.value > 0.5f)
                {
                    this.room.AddObject(new Bubble(base.firstChunk.pos, base.firstChunk.vel + Custom.DegToVec(UnityEngine.Random.value * 360f) * Mathf.Lerp(6f, 0f, this.airInLungs), false, false));
                }
            }
            this.submerged = true;
        }
        else
        {
            if (this.submerged && this.airInLungs < 0.333333343f)
            {
                this.lungsExhausted = true;
            }
            if (!this.lungsExhausted && this.airInLungs > 0.9f)
            {
                this.airInLungs = 1f;
            }
            if (this.airInLungs <= 0f)
            {
                this.airInLungs = 0f;
            }
            this.airInLungs += 1f / (float)((!this.lungsExhausted) ? 60 : 240);
            if (this.airInLungs >= 1f)
            {
                this.airInLungs = 1f;
                this.lungsExhausted = false;
                this.drown = 0f;
            }
            this.submerged = false;
            if (this.lungsExhausted && this.animation != Player.AnimationIndex.SurfaceSwim)
            {
                this.swimCycle += 0.1f;
            }
        }
        if (this.lungsExhausted)
        {
            if (this.slowMovementStun < 5)
            {
                this.slowMovementStun = 5;
            }
            if (this.drown > 0f && this.slowMovementStun < 10)
            {
                this.slowMovementStun = 10;
            }
        }
    }
    public PearlConversation pearlConversation;

    public int dropCounter = 0;
}


