using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BloomPostprocess;
using DwarfCorp.Gui;
using DwarfCorp.Gui.Widgets;
using DwarfCorp.Tutorial;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using DwarfCorp.GameStates;
using Newtonsoft.Json;
using DwarfCorp.Events;
using System.Diagnostics;

namespace DwarfCorp
{
    public partial class PersistentWorldData
    {
        public struct ApplicantArrival
        {
            public Applicant Applicant;
            public DateTime ArrivalTime;
        }

        public List<ApplicantArrival> NewArrivals = new List<ApplicantArrival>();
        public List<CreatureAI> SelectedMinions = new List<CreatureAI>();
    }

    public partial class WorldManager
    {
        public DateTime Hire(Applicant currentApplicant, int delay)
        {
            var startDate = Time.CurrentDate;
            if (PersistentData.NewArrivals.Count > 0)
                startDate = PersistentData.NewArrivals.Last().ArrivalTime;

            PersistentData.NewArrivals.Add(new PersistentWorldData.ApplicantArrival()
            {
                Applicant = currentApplicant,
                ArrivalTime = startDate + new TimeSpan(0, delay, 0, 0, 0)
            });

            PlayerFaction.AddMoney(-(decimal)(currentApplicant.Level.Pay * 4));
            return PersistentData.NewArrivals.Last().ArrivalTime;
        }

        public void HireImmediately(Applicant currentApplicant)
        {
            var rooms = EnumerateZones().Where(room => room.Type.Name == "Balloon Port").ToList();
            Vector3 spawnLoc = Renderer.Camera.Position;
            if (rooms.Count > 0)
            {
                spawnLoc = rooms.First().GetBoundingBox().Center() + Vector3.UnitY * 15;
            }

            var dwarfPhysics = DwarfFactory.GenerateDwarf(
                    spawnLoc,
                    ComponentManager, currentApplicant.ClassName, currentApplicant.LevelIndex, currentApplicant.Gender, currentApplicant.RandomSeed);
            ComponentManager.RootComponent.AddChild(dwarfPhysics);
            var newMinion = dwarfPhysics.EnumerateAll().OfType<Dwarf>().FirstOrDefault();
            Debug.Assert(newMinion != null);

            newMinion.Stats.AllowedTasks = currentApplicant.Class.Actions;
            newMinion.Stats.LevelIndex = currentApplicant.LevelIndex - 1;
            newMinion.Stats.LevelUp(newMinion);
            newMinion.Stats.FullName = currentApplicant.Name;
            newMinion.AI.AddMoney(currentApplicant.Level.Pay * 4m);
            newMinion.AI.Biography = currentApplicant.Biography;

            MakeAnnouncement(
                new Gui.Widgets.QueuedAnnouncement
                {
                    Text = String.Format("{0} was hired as a {1}.", currentApplicant.Name, currentApplicant.Level.Name),
                    ClickAction = (gui, sender) => newMinion.AI.ZoomToMe()
                });

            SoundManager.PlaySound(ContentPaths.Audio.Oscar.sfx_gui_positive_generic, 0.15f);
        }

        public void FireEmployee(CreatureAI Employee)
        {
            PlayerFaction.Minions.Remove(Employee);
            PersistentData.SelectedMinions.Remove(Employee);
            PlayerFaction.AddMoney(-(decimal)(Employee.Stats.CurrentLevel.Pay * 4));
        }

        public int CalculateSupervisionCap()
        {
            return PlayerFaction.Minions.Sum(c => c.Stats.CurrentClass.Managerial ? (int)c.Stats.Intelligence : 0) + 4;
        }

        public int CalculateSupervisedEmployees()
        {
            return PlayerFaction.Minions.Where(c => !c.Stats.CurrentClass.Managerial).Count() + PersistentData.NewArrivals.Where(c => !c.Applicant.Class.Managerial).Count();
        }

        public void PayEmployees()
        {
            DwarfBux total = 0;
            bool noMoney = false;
            foreach (CreatureAI creature in PlayerFaction.Minions)
            {
                if (creature.Stats.IsOverQualified)
                    creature.Creature.AddThought(Thought.ThoughtType.IsOverQualified);

                var thoughts = creature.Physics.GetComponent<DwarfThoughts>();

                if (thoughts != null)
                    thoughts.Thoughts.RemoveAll(thought => thought.Description.Contains("paid"));

                DwarfBux pay = creature.Stats.CurrentLevel.Pay;
                total += pay;

                if (total >= PlayerFaction.Economy.Funds)
                {
                    if (!noMoney)
                    {
                        MakeAnnouncement("We have no money!");
                        Tutorial("money");
                        SoundManager.PlaySound(ContentPaths.Audio.Oscar.sfx_gui_negative_generic, 0.5f);
                    }
                    noMoney = true;
                }
                else
                {
                    creature.Creature.AddThought(Thought.ThoughtType.GotPaid);
                }

                creature.AssignTask(new ActWrapperTask(new GetMoneyAct(creature, pay)) { AutoRetry = true, Name = "Get paid.", Priority = Task.PriorityType.High });
            }

            MakeAnnouncement(String.Format("We paid our employees {0} today.", total));
            SoundManager.PlaySound(ContentPaths.Audio.change, 0.15f);
            Tutorial("pay");
        }

        public bool AreAllEmployeesAsleep()
        {
            return (PlayerFaction.Minions.Count > 0) && PlayerFaction.Minions.All(minion => !minion.Active || ((!minion.Stats.Species.CanSleep || minion.Creature.Stats.IsAsleep) && !minion.IsDead));
        }
    }
}
