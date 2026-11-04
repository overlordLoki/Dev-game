using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything that walks around: position and movement, the tight
    /// collision boxes from bounds.json, push-out collision, and drawing the current
    /// animation from a sprite sheet. Subclasses register their animations in the
    /// constructor and override only what's different about them (Player reads the
    /// keyboard; NPCs wander - see NPC).
    ///
    /// Everything is in world units - cells - not pixels: Position, Width/Height, Speed,
    /// Boxes and Bounds. Only Draw converts to pixels (Layout.ToScreen), so a window resize
    /// changes how big an entity looks, never where it is or how fast it walks.
    /// </summary>
    public abstract class Entity : IEntity
    {
        public int Id { get; }
        /// <summary>The sprite's top-left corner, in cells.</summary>
        public Vector2 Position { get; set; }
        /// <summary>Cells per second.</summary>
        public float Speed { get; set; }
        public Vector2 Direction { get; set; }

        // How tall the drawn frame is, in cells. Width follows from the sheet's frame
        // aspect ratio, so a non-square sheet (the knight's 96x84) isn't squashed.
        // (The editor reads this override from the source, so keep its name and shape.)
        protected virtual float SizeInCells => 0.9f;

        /// <summary>The drawn sprite's height, in cells.</summary>
        public float Height => SizeInCells;
        /// <summary>The drawn sprite's width, in cells: the height times the frame's aspect ratio.</summary>
        public float Width
        {
            get
            {
                if (!Animations.TryGetValue(CurrentAnim, out var anim)) return Height;
                return Height * anim.FrameWidth / anim.FrameHeight;
            }
        }

        // Depth-sort key: the feet, i.e. the bottom of the collision box. Not the sprite's
        // bottom - sprites have transparent padding under the feet, which made entities sort
        // as if they stood lower than they do (drawn in front of a tree they're behind).
        // No boxes in bounds.json -> fall back to the sprite's bottom.
        public float SortY
        {
            get
            {
                RectF feet = Bounds;
                return feet.IsEmpty ? Position.Y + Height : feet.Bottom;
            }
        }

        protected Dictionary<string, Animation> Animations { get; } = new();
        /// <summary>The name of the animation playing now ("idle", "walk_up"...).</summary>
        public string CurrentAnim { get; private set; } = "idle";

        /// <summary>Which way this entity looks: whichever axis it last moved further along.</summary>
        public Facing Facing { get; protected set; } = Facing.Down;

        // The last way it faced along X. Side-only art (the knight) keeps showing this while
        // walking straight up or down, instead of snapping back to the right.
        protected bool FacedLeft { get; private set; }

        // Sheets are drawn facing right; this flips them. Side-only art flips by the last
        // left/right; Player, which has up and down sheets, flips only when facing right.
        protected virtual bool FlipSprite => FacedLeft;

        /// <summary>How much more this can take; null for something that can't be hurt.</summary>
        public Health Health { get; private set; }
        public bool IsDead => Health?.IsDead == true;

        /// <summary>
        /// Whether reaching 0 health is the end. True by default (a barrel, later an enemy); the
        /// player respawns and named NPCs are only knocked out, so both say false. A fight that
        /// ends with this false is posted as KnockedOut, not Killed.
        /// </summary>
        public virtual bool DiesForGood => true;

        /// <summary>
        /// Gives this entity a full health of <paramref name="max"/>, and hooks its death and revival
        /// up to <see cref="OnDied"/> and <see cref="OnRevived"/>. Subclasses call it from their
        /// constructor; public so tests (and later, things the level spawns) can too.
        /// </summary>
        public void SetHealth(int max)
        {
            if (Health != null) { Health.Died -= OnDied; Health.Revived -= OnRevived; }
            Health = new Health(max);
            Health.Died += OnDied;
            Health.Revived += OnRevived;
        }

        /// <summary>Health reached 0. The subclass stops whatever it was doing and plays its death clip.</summary>
        protected virtual void OnDied()
        {
            Direction = Vector2.Zero;
            Swing = null;   // the dead don't finish their swing
            Guard = 0;
        }
        /// <summary>Healed back from 0.</summary>
        protected virtual void OnRevived() { }

        // ------------------------------------------------------------------ combat

        /// <summary>The attack in progress, if any. Ticked by Update; CombatSystem resolves its hits.</summary>
        public Swing Swing { get; private set; }

        /// <summary>
        /// Starts <paramref name="attack"/> in the way this entity faces. False (and nothing starts)
        /// if it's dead or already swinging. It can't move until the swing is done.
        /// </summary>
        public bool StartSwing(AttackData attack)
        {
            if (IsDead || Swing != null) return false;
            Swing = new Swing(attack, this, Facing);
            return true;
        }

        /// <summary>Seconds left of guarding: rooted, and blocking hits from in front (see <see cref="Blocks"/>).</summary>
        public float Guard { get; private set; }
        public bool IsGuarding => Guard > 0;

        /// <summary>Guards for <paramref name="seconds"/>, facing the way it faces now. False if dead or swinging.</summary>
        public bool StartGuard(float seconds)
        {
            if (IsDead || Swing != null || seconds <= 0) return false;
            Guard = seconds;
            return true;
        }

        /// <summary>
        /// Whether a guard stops <paramref name="swing"/>: guarding, and the swing comes at the
        /// front (thrown the opposite way to this entity's facing). From behind or the side it lands.
        /// </summary>
        public bool Blocks(Swing swing) => IsGuarding && swing.Facing.ToVector() == -Facing.ToVector();

        /// <summary>
        /// Turns to face <paramref name="point"/>, along whichever axis it's further away on,
        /// without moving. A fighter aims its swing at someone this way.
        /// </summary>
        public void FaceTowards(Vector2 point)
        {
            Vector2 away = point - Bounds.Center;
            if (away == Vector2.Zero) return;
            Facing = FacingOf(away);
            if (away.X != 0) FacedLeft = away.X < 0;
        }

        /// <summary>The facing along whichever axis <paramref name="d"/> is longer on; sideways on a tie.</summary>
        public static Facing FacingOf(Vector2 d) =>
            Math.Abs(d.Y) > Math.Abs(d.X) ? (d.Y < 0 ? Facing.Up : Facing.Down)
                                          : (d.X < 0 ? Facing.Left : Facing.Right);

        /// <summary>How long the red flash and the hurt clip last after a hit lands, in seconds.</summary>
        public const float FlinchSeconds = 0.25f;
        /// <summary>Seconds left of flinching from the last hit.</summary>
        public float Flinch { get; private set; }
        public bool IsFlinching => Flinch > 0;
        public void StartFlinch() => Flinch = FlinchSeconds;
        // What a flinching sprite is multiplied by: a red flash that still shows the sprite.
        private static readonly Color HitTint = new(255, 90, 90);

        /// <summary>How long a knockback takes, in seconds. It starts fast and eases to a stop.</summary>
        public const float KnockbackSeconds = 0.15f;
        private Vector2 _knockDirection;
        private float _knockDistance, _knockTime = KnockbackSeconds;

        /// <summary>
        /// Pushes this entity <paramref name="distance"/> cells along <paramref name="direction"/>
        /// over <see cref="KnockbackSeconds"/>. Collision and the map edge still apply, since the
        /// World resolves them after each Update, so nobody is knocked into a wall.
        /// </summary>
        public void Knock(Vector2 direction, float distance)
        {
            if (direction == Vector2.Zero || distance <= 0) return;
            _knockDirection = Vector2.Normalize(direction);
            _knockDistance = distance;
            _knockTime = 0;
        }

        // Moves by however much of the knockback this step covers. Eased (1 - (1 - t)^2), and
        // worked out from where it was and where it is, so the total is exact at any frame rate.
        private void ApplyKnock(float dt)
        {
            if (_knockTime >= KnockbackSeconds) return;
            static float Covered(float t) { float left = 1 - t / KnockbackSeconds; return 1 - left * left; }

            float before = Covered(_knockTime);
            _knockTime = Math.Min(KnockbackSeconds, _knockTime + dt);
            Position += _knockDirection * _knockDistance * (Covered(_knockTime) - before);
        }

        protected Entity(Vector2 position, int id)
        {
            Position = position;
            Id = id;
        }

        // ---------------------------------------------------------------- collision

        // Key under Content/bounds.json. The class name, so Player and Knight each get
        // their own tuned boxes without saying so.
        protected virtual string BoundsKey => GetType().Name;

        // Tight collision box(es) around the body/feet. The sprite frame has transparent
        // padding, so these are tuned by eye in the editor against the debug outline and
        // stored in Content/bounds.json (basis = the sprite's own size).
        public IReadOnlyList<RectF> Boxes
        {
            get
            {
                var boxes = BoundsStore.For(BoundsKey);
                var rects = new RectF[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRect(Position.X, Position.Y, Width, Height);
                return rects;
            }
        }

        public RectF Bounds => Geometry.Union(Boxes);

        /// <summary>
        /// Where this entity stands, in cells: the bottom centre of its collision box, or of the
        /// sprite if it has no boxes. The one idea of "where it is" for anything about standing
        /// somewhere - spawning on a point, walking to one, `where` - so they can't drift apart.
        /// (Depth sorting, doors and the map edge measure the same box.)
        /// </summary>
        public Vector2 Feet
        {
            get
            {
                RectF body = Bounds;
                if (body.IsEmpty) body = new RectF(Position.X, Position.Y, Width, Height);
                return new Vector2(body.Center.X, body.Bottom);
            }
        }

        /// <summary>
        /// Moves the entity so its <see cref="Feet"/> are on <paramref name="spot"/>. A sprite
        /// with lots of padding above the body (the knight's) still lands standing on the spot,
        /// rather than with its frame centred there and its feet sticking out below.
        /// </summary>
        public void StandAt(Vector2 spot) => Position += spot - Feet;

        /// <summary>
        /// Push out of another entity, tight box against tight box. NPC overrides this
        /// to also bounce away.
        /// </summary>
        public virtual void CheckEntityCollision(IEntity other) => ResolveCollision(other.Bounds);

        /// <summary>
        /// Push out of a multi-box obstacle. Resolving against every overlapping box in
        /// one frame makes them fight and jitter, so this picks the single deepest overlap
        /// and resolves against that one - the rest sort themselves out over later frames.
        /// </summary>
        public void ResolveCollision(IReadOnlyList<RectF> others)
        {
            RectF me = Bounds;
            RectF deepest = RectF.Empty;
            float best = 0f;

            foreach (var other in others)
            {
                if (!me.Intersects(other)) continue;
                RectF o = RectF.Intersect(me, other);
                float penetration = Math.Min(o.Width, o.Height);
                if (penetration > best) { best = penetration; deepest = other; }
            }

            if (best > 0f) ResolveCollision(deepest);
        }

        /// <summary>
        /// Something stopped this entity along one axis (<paramref name="horizontal"/> = along
        /// X): another entity, or the edge of the map. Does nothing by default - the player just
        /// stops. NPC overrides it to let its brain decide (a wanderer turns around).
        /// </summary>
        public virtual void OnBlocked(bool horizontal) { }

        /// <summary>Push out of one rectangle along the shallower axis, no bounce.</summary>
        public void ResolveCollision(RectF other)
        {
            RectF me = Bounds;
            if (!me.Intersects(other)) return;

            var (pushX, pushY) = Overlap(me, other);
            if (Math.Abs(pushX) < Math.Abs(pushY))
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);
        }

        // How far to move `me` on each axis to get it out of `other`, signed.
        protected static (float pushX, float pushY) Overlap(RectF me, RectF other)
        {
            float overlapLeft   = me.Right  - other.Left;
            float overlapRight  = other.Right - me.Left;
            float overlapTop    = me.Bottom - other.Top;
            float overlapBottom = other.Bottom - me.Top;

            float pushX = overlapLeft < overlapRight ? -overlapLeft : overlapRight;
            float pushY = overlapTop  < overlapBottom ? -overlapTop : overlapBottom;
            return (pushX, pushY);
        }

        // ------------------------------------------------------------ update / move

        public virtual void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Health?.Update(dt);
            Flinch = Math.Max(0f, Flinch - dt);
            Guard = Math.Max(0f, Guard - dt);

            // A swing roots its attacker to the spot from windup to the end of recovery.
            Swing?.Update(dt);
            if (Swing?.IsDone == true) Swing = null;

            // The dead lie still; only their (one-shot) death clip plays on. Guarding roots too.
            if (!IsDead && Swing == null && !IsGuarding)
            {
                Vector2 before = Position;
                Move(gameTime);
                Face(Position - before);
            }

            // After facing, so being shoved backwards doesn't turn anyone round. Dead or not:
            // a killing blow still knocks the body back.
            ApplyKnock(dt);

            if (Animations.TryGetValue(CurrentAnim, out var anim))
                anim.Update(gameTime);
        }

        /// <summary>
        /// Faces whichever axis <paramref name="moved"/> goes further along; standing still keeps
        /// the last facing. An exact diagonal keeps the current facing if it's one of the two
        /// ways being walked (so holding Right then adding Down doesn't turn the player), and
        /// otherwise faces sideways.
        /// </summary>
        protected void Face(Vector2 moved)
        {
            if (moved == Vector2.Zero) return;

            float ax = Math.Abs(moved.X), ay = Math.Abs(moved.Y);
            Facing side = moved.X < 0 ? Facing.Left : Facing.Right;
            Facing vertical = moved.Y < 0 ? Facing.Up : Facing.Down;

            if (ax > ay) Facing = side;
            else if (ay > ax) Facing = vertical;
            else if (Facing != side && Facing != vertical) Facing = side;

            if (moved.X != 0) FacedLeft = moved.X < 0;
        }

        // Default movement: straight along Direction at Speed.
        protected virtual void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Position += Direction * Speed * delta;
        }

        // --------------------------------------------------------------- animation

        /// <summary>
        /// Loads a sheet through Textures and registers it under <paramref name="name"/>.
        /// Call from the subclass constructor, one line per animation.
        /// </summary>
        protected void AddAnimation(string name, SpriteSheetInfo sheet, double frameDuration, bool loop = true)
        {
            var texture = Textures.Get(sheet.Path);
            Animations[name] = new Animation(texture, sheet.FrameCount, sheet.FrameWidth,
                                             sheet.FrameHeight, frameDuration) { Loop = loop };
        }

        /// <summary>
        /// Switches animation, starting the new one from its first frame. Asking for the
        /// one already playing does nothing, so this is safe to call every frame.
        /// </summary>
        protected void Play(string name)
        {
            if (name == CurrentAnim || !Animations.ContainsKey(name)) return;
            CurrentAnim = name;
            Animations[name].Reset();
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (Animations.TryGetValue(CurrentAnim, out var anim))
            {
                // The one place an entity turns into pixels.
                var dest = Layout.ToScreen(new RectF(Position.X, Position.Y, Width, Height));
                var fx = FlipSprite ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                anim.Draw(spriteBatch, dest, fx, IsFlinching ? HitTint : Color.White);
            }

            // Debug: the live hitbox in red (only with Debug.SHOWBOUNDS).
            if (Swing?.IsLive == true) Debug.DrawRect(spriteBatch, Layout.ToScreen(Swing.Hitbox), Color.Red);

            // Debug: outline the collision body (only shows with Debug.SHOWBOUNDS).
            Debug.DrawRect(spriteBatch, Layout.ToScreen(Bounds), Color.Black);
        }
        // The nearest entity within maxDistance (in cells) of this one, used for AI and talking.
        public Entity GetNearestEntityInRange(IEnumerable<Entity> entities, float maxDistance)
        {
            Entity nearest = null;
            float closestDistance = float.MaxValue;
            // Measured body to body: Position is the frame's top-left corner, which sits
            // further from the body the bigger the sprite is.
            Vector2 me = Bounds.Center;

            foreach (var entity in entities)
            {
                if (entity == this) continue;

                float distance = Vector2.Distance(me, entity.Bounds.Center);
                if (distance < maxDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }
    }
}
