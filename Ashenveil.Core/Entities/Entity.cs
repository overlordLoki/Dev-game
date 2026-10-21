using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
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
        protected string CurrentAnim { get; private set; } = "idle";

        // Sheets are drawn facing right; this flips them when moving left.
        protected bool FacingLeft { get; set; }

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
            float oldX = Position.X;
            Move(gameTime);

            // Face whichever way we actually moved; standing still keeps the last facing.
            if (Position.X < oldX) FacingLeft = true;
            else if (Position.X > oldX) FacingLeft = false;

            if (Animations.TryGetValue(CurrentAnim, out var anim))
                anim.Update(gameTime);
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
                var fx = FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                anim.Draw(spriteBatch, dest, fx);
            }

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
