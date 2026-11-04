using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// Turns a fight into one event, not one per hit. It listens to <see cref="CombatSystem"/>'s
    /// Hit and Blocked, keeps a tally per pair of people, and posts a single Attacked (Killed, KnockedOut)
    /// event when the fight is over: someone died, they've gone quiet for
    /// <see cref="QuietSeconds"/>, or they're no longer in the same level.
    ///
    /// Posted like any other event, so perception makes it a hot memory for whoever saw it, and
    /// the day-end reflection turns it into lasting opinions. It only counts blows: it doesn't
    /// care what either brain is doing, so a hit-and-run, a fight that's all blocks, or one NPC
    /// hitting another all get recorded the same way.
    /// </summary>
    public class FightRecorder
    {
        /// <summary>
        /// A fight with no hit or block for this long, in seconds, is over. Longer than an NPC's
        /// give-up time plus a cooldown, so a chase with gaps between swings stays one fight.
        /// </summary>
        public const float QuietSeconds = 10f;

        private readonly Func<Entity, string> _levelOf;
        private readonly Func<GameEvent, GameEvent> _post;
        private readonly List<Fight> _open = new();

        /// <param name="levelOf">The level someone is in (null if they're nowhere).</param>
        /// <param name="post">Posts a finished fight (EventLog.Post).</param>
        public FightRecorder(Func<Entity, string> levelOf, Func<GameEvent, GameEvent> post)
        {
            _levelOf = levelOf;
            _post = post;
        }

        /// <summary>Fights still going, oldest first. For tests and the console.</summary>
        public IReadOnlyList<Fight> Open => _open;

        /// <summary>Listens to <paramref name="combat"/>'s hits and blocks.</summary>
        public void Watch(CombatSystem combat)
        {
            combat.Hit += (attacker, victim, damage) => Landed(attacker, victim, damage);
            combat.Blocked += (attacker, defender) => Blocked(attacker, defender);
        }

        /// <summary>A blow landed: counted against the fight between the two, started if there isn't one.</summary>
        public void Landed(Entity attacker, Entity victim, int damage)
        {
            var fight = FightOf(attacker, victim);
            fight.Count(attacker).Blows++;
            fight.Count(attacker).Damage += damage;
            fight.Quiet = 0f;
        }

        /// <summary>A blow was blocked: counted for the defender, and it still keeps the fight going.</summary>
        public void Blocked(Entity attacker, Entity defender)
        {
            var fight = FightOf(attacker, defender);
            fight.Count(defender).Blocks++;
            fight.Quiet = 0f;
        }

        /// <summary>
        /// Counts quiet time and posts every fight that's over. The World calls it after
        /// CombatSystem.Update, so a killing blow is posted the same frame it lands.
        /// </summary>
        public void Update(float dt)
        {
            foreach (var fight in _open.ToList())
            {
                fight.Quiet += dt;
                bool over = fight.Started.IsDead || fight.Other.IsDead || fight.Quiet >= QuietSeconds
                            || _levelOf(fight.Started) != _levelOf(fight.Other);
                if (over) End(fight);
            }
        }

        /// <summary>
        /// Posts every open fight now, finished or not. Fights in progress aren't saved, so this
        /// runs before a save: otherwise an attack mid-fight would never become an event.
        /// </summary>
        public void EndAll()
        {
            foreach (var fight in _open.ToList()) End(fight);
        }

        // The fight between these two, either way round, or a new one with `attacker` as its starter.
        private Fight FightOf(Entity attacker, Entity victim)
        {
            var fight = _open.FirstOrDefault(f => f.Is(attacker, victim));
            if (fight != null) return fight;

            fight = new Fight(attacker, victim, _levelOf(victim), victim.Feet);
            _open.Add(fight);
            return fight;
        }

        private void End(Fight fight)
        {
            _open.Remove(fight);
            _post(fight.ToEvent());
        }

        /// <summary>One side's tally.</summary>
        public class Side
        {
            public int Blows, Damage, Blocks;
        }

        /// <summary>
        /// Two people fighting: who threw the first blow, at whom, where, and what each did since.
        /// </summary>
        public class Fight
        {
            public Entity Started { get; }
            public Entity Other { get; }
            /// <summary>Where it began: the level and the feet of whoever was hit first.</summary>
            public string Level { get; }
            public Vector2 Where { get; }
            public Side ByStarter { get; } = new();
            public Side ByOther { get; } = new();
            /// <summary>Seconds since the last hit or block.</summary>
            public float Quiet { get; internal set; }

            public Fight(Entity started, Entity other, string level, Vector2 where)
            {
                Started = started;
                Other = other;
                Level = level;
                Where = where;
            }

            public bool Is(Entity a, Entity b) => (a == Started && b == Other) || (a == Other && b == Started);

            public Side Count(Entity who) => who == Started ? ByStarter : ByOther;

            /// <summary>
            /// The fight as one event. Someone going down makes it Killed, or KnockedOut if they
            /// get up again (<see cref="Entity.DiesForGood"/>), with the one left standing as actor;
            /// otherwise Attacked, with the starter as actor whoever did more damage, so a grudge
            /// is held against whoever began it, never against someone defending themselves.
            /// </summary>
            public GameEvent ToEvent()
            {
                string text = Describe();
                if (Other.IsDead && !Started.IsDead) return Downed(Started, Other, text);
                if (Started.IsDead && !Other.IsDead) return Downed(Other, Started, text);
                return GameEvent.Of(EventType.Attacked, Level, Where, Started.Id, Other.Id, text);
            }

            private GameEvent Downed(Entity by, Entity who, string text) =>
                GameEvent.Of(who.DiesForGood ? EventType.Killed : EventType.KnockedOut, Level, Where, by.Id, who.Id, text);

            private static string Fell(Entity who) => who.DiesForGood ? "was killed" : "was knocked out";

            // "Loki attacked Sir Lancelot, who fought back. Loki landed 3 blows, Sir Lancelot 2.
            // Sir Lancelot was killed." Neutral, like every event's text.
            private string Describe()
            {
                string a = Conversation.NameOf(Started), b = Conversation.NameOf(Other);
                var parts = new List<string>();

                if (ByOther.Blows > 0)
                {
                    parts.Add($"{a} attacked {b}, who fought back.");
                    parts.Add($"{a} landed {Blows(ByStarter.Blows)}, {b} {ByOther.Blows}.");
                }
                else if (ByStarter.Blows > 0)
                    parts.Add($"{a} attacked {b}, landing {Blows(ByStarter.Blows)}.");
                else
                    parts.Add($"{a} attacked {b}, who blocked every blow.");

                if (ByOther.Blocks > 0 && ByStarter.Blows > 0) parts.Add($"{b} blocked {ByOther.Blocks}.");
                if (ByStarter.Blocks > 0) parts.Add($"{a} blocked {ByStarter.Blocks}.");
                if (Other.IsDead) parts.Add($"{b} {Fell(Other)}.");
                if (Started.IsDead) parts.Add($"{a} {Fell(Started)}.");
                return string.Join(" ", parts);
            }

            private static string Blows(int n) => n switch { 0 => "no blows", 1 => "1 blow", _ => $"{n} blows" };
        }
    }
}
