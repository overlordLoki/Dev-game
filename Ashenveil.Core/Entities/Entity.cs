using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything that walks around: position and movement, the tight
    /// collision boxes from bounds.json, push-out collision, and drawing the current
    /// animation from a sprite sheet. Subclasses register their animations in the
    /// constructor and override only what's different about them (Player reads the
    /// keyboard; NPCs wander - see NPC).
    /// </summary>
    public abstract class Entity : IEntity
    {
        public int Id { get; }
        public Vector2 Position { get; set; }
        public float Speed { get; set; }
        public Vector2 Direction { get; set; }

        // How tall the drawn frame is, in cells. Width follows from the sheet's frame
        // aspect ratio, so a non-square sheet (the knight's 96x84) isn't squashed.
        protected virtual float SizeInCells => 0.9f;

        public int Height => (int)(Layout.CellSize * SizeInCells);
        public int Width
        {
            get
            {
                if (!Animations.TryGetValue(CurrentAnim, out var anim)) return Height;
                return (int)(Height * anim.FrameWidth / (float)anim.FrameHeight);
            }
        }

        // Depth-sort key: the feet = bottom of the entity's box.
        public int SortY => (int)(Position.Y + Height);

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
        public IReadOnlyList<Rectangle> Boxes
        {
            get
            {
                var boxes = BoundsStore.For(BoundsKey);
                var rects = new Rectangle[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRectangle((int)Position.X, (int)Position.Y, Width, Height);
                return rects;
            }
        }

        public Rectangle Bounds => Geometry.Union(Boxes);

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
        public void ResolveCollision(IReadOnlyList<Rectangle> others)
        {
            Rectangle me = Bounds;
            Rectangle deepest = Rectangle.Empty;
            float best = 0f;

            foreach (var other in others)
            {
                if (!me.Intersects(other)) continue;
                Rectangle o = Rectangle.Intersect(me, other);
                float penetration = Math.Min(o.Width, o.Height);
                if (penetration > best) { best = penetration; deepest = other; }
            }

            if (best > 0f) ResolveCollision(deepest);
        }

        /// <summary>Push out of one rectangle along the shallower axis, no bounce.</summary>
        public void ResolveCollision(Rectangle other)
        {
            Rectangle me = Bounds;
            if (!me.Intersects(other)) return;

            var (pushX, pushY) = Overlap(me, other);
            if (Math.Abs(pushX) < Math.Abs(pushY))
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);
        }

        // How far to move `me` on each axis to get it out of `other`, signed.
        protected static (float pushX, float pushY) Overlap(Rectangle me, Rectangle other)
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
                var dest = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
                var fx = FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                anim.Draw(spriteBatch, dest, fx);
            }

            // Debug: outline the collision body (only shows with Debug.SHOWBOUNDS).
            Debug.DrawRect(spriteBatch, Bounds, Color.Black);
        }
    }
}
