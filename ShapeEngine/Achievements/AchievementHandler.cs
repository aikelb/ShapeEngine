﻿using ShapeEngine.Core;
using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.StaticLib;
using ShapeEngine.Core.Shapes;
using ShapeEngine.StaticLib.Drawing;
using ShapeEngine.Text;

namespace ShapeEngine.Achievements
{
    public class AchievementStat
    {
        public string apiName = "";
        public string displayName = "";

        public int value = 0;
        public int defaultValue = 0;
        public int maxValue = int.MaxValue;
        public int minValue = int.MinValue;


        public event Action<int, int>? OnValueChanged;

        public AchievementStat(string apiName, string displayName, int defaultValue, int maxValue = int.MaxValue, int minValue = int.MinValue)
        {
            this.apiName = apiName;
            this.displayName = displayName;
            this.maxValue = maxValue;
            this.minValue = minValue;
            this.defaultValue = defaultValue;
            if (this.defaultValue < minValue) this.defaultValue = minValue;
            else if (this.defaultValue > maxValue) this.defaultValue = maxValue;
            this.value = this.defaultValue;
        }

        public void Reset() { SetStat(defaultValue); }
        public void ChangeStat(int change)
        {
            if (change == 0) return;

            int oldValue = value;
            value += change;
            if (value < minValue) value = minValue;
            else if (value > maxValue) value = maxValue;

            if (oldValue != value) OnValueChanged?.Invoke(oldValue, value);
        }

        public void SetStat(int newValue) { ChangeStat(newValue - value); }

    }
    
    public class Achievement
    {
        public event Action<Achievement>? Achieved;
        public event Action<Achievement>? IncrementNotification;

        public string apiName = "";
        public string displayName = "";

        protected bool achieved = false;
        protected bool hidden = false; //doesnt show description and display name

        protected AchievementStat stat;
        protected int start;
        protected int end;
        protected int notificationIncrement = 1;

        public Achievement(string apiName, string displayName, bool hidden, AchievementStat stat, int start, int end, int notificationIncrement = 1)
        {
            this.apiName = apiName;
            this.displayName = displayName;
            this.hidden = hidden;

            this.stat = stat;
            this.start = start;
            this.end = end;
            this.notificationIncrement = notificationIncrement;

            if(IsGoalFinished())
            {
                achieved = true;
            }
            else
            {
                this.stat.OnValueChanged += OnStatValueChanged;
            }
        }



        protected void OnStatValueChanged(int oldValue, int newValue)
        { 
            if(newValue >= end)
            {
                this.stat.OnValueChanged-= OnStatValueChanged;
                Achieve();
            }
            else
            {
                if(newValue >= start && notificationIncrement > 0 && newValue != oldValue && newValue % notificationIncrement == 0)
                {
                    IncrementNotification?.Invoke(this);
                }
            }
        }


        public bool IsGoalActive() { return stat.value >= start; }
        public bool IsGoalFinished() { return stat.value >= end; }
        public float GetGoalPercentage() { return ShapeMath.Clamp( (float)(stat.value - start) / (float)(end - start), 0f, 1f); }

        public bool IsHidden() { return hidden; }
        public bool IsAchieved() { return achieved; }
        public void Achieve() 
        {
            if (!achieved) Achieved?.Invoke(this);
            achieved = true; 
        }
        public virtual void Draw(TextFont textFont, Rect rect, ColorRgba bgColorRgba, ColorRgba textColorRgba, ColorRgba progressColorRgba, ColorRgba achievedColorRgba)
        {
            Rect left = new(rect.X, rect.Y, rect.Width * 0.25f, rect.Height);
            Rect leftTop = new(left.X, left.Y, left.Width, left.Height * 0.5f);
            Rect leftBottom = new(left.X, left.Y +left.Height * 0.5f, left.Width, left.Height * 0.5f);
            Rect right = new(rect.X + rect.Width * 0.28f, rect.Y, rect.Width * 0.72f, rect.Height);
            rect.DrawBar(GetGoalPercentage(), progressColorRgba, bgColorRgba);
            if (achieved) rect.DrawLines(3f, achievedColorRgba);// SDrawing.DrawRect(rect, new(0f), 0f, 3f, achievedColor);
            int value = stat.value;
            int max = end;

            textFont.ColorRgba = textColorRgba;
            textFont.DrawTextWrapNone($"{value}", leftTop, new(0.5f));
            textFont.DrawTextWrapNone($"{max}", leftBottom, new(0.5f));
            // ShapeText.DrawText(font, String.Format("{0}", value), leftTop, 1f, new(0.5f) ,textColor);
            // ShapeText.DrawText(font, String.Format("{0}", max), leftBottom, 1f, new(0.5f),textColor);
            if (hidden)
            {
                
                if(achieved) textFont.DrawTextWrapNone(displayName, right, new(0.5f), achieved ? achievedColorRgba : textColorRgba); // ShapeText.DrawText(font, displayName, right, 1f, new(0.5f), achieved ? achievedColor : textColor);
                else textFont.DrawTextWrapNone("???", right, new(0.5f), textColorRgba); //ShapeText.DrawText(font, "???", right, 1f, new(0.5f), textColor);
            }
            else
            {
                textFont.ColorRgba = achieved ? achievedColorRgba : textColorRgba;
                textFont.DrawTextWrapNone(displayName, leftBottom, new(0.5f));
            }
        }
    }

    internal class AchievmentDrawStack
    {
        public float duration;
        public Achievement achievement;
        public AchievmentDrawStack(float duration, Achievement achievement)
        {
            this.duration = duration;
            this.achievement = achievement;
        }
        
        public void Update(float dt)
        {
            if (duration <= 0f) return;

            duration -= dt;
        }
        public bool IsFinished() { return duration <= 0f; }
    }

    public class AchievementHandler
    {
        private Dictionary<string,AchievementStat> stats = new();
        private List<Achievement> achievements = new();

        private List<AchievmentDrawStack> achievementDrawStack = new();
        private TextFont textFont;

        public AchievementHandler(TextFont textFont)
        {
            this.textFont = textFont;
        }

        public float achievedDisplayDuration = 5f;
        public float notificationDuration = 3f;

        public void Update(float dt)
        {
            if(achievementDrawStack.Count > 0)
            {
                achievementDrawStack[0].Update(dt);
                if (achievementDrawStack[0].IsFinished())
                {
                    achievementDrawStack.RemoveAt(0);
                }

            }
        }
        public void Draw(Rect achievementRect, ColorRgba background, ColorRgba text, ColorRgba progress, ColorRgba achieved) 
        {
            if (achievementDrawStack.Count > 0)
            {
                achievementDrawStack[0].achievement.Draw(textFont, achievementRect, background, text, progress, achieved);
            }
        }
        public void Close()
        {
            stats.Clear();
            achievements.Clear();
            achievementDrawStack.Clear();
        }
        public  void AddStat(AchievementStat stat)
        {
            if(stats.ContainsKey(stat.apiName)) return;
            stats.Add(stat.apiName, stat);
        }
        public  int GetStatValue(string stat)
        {
            if(!stats.ContainsKey(stat)) return -1;
            else return stats[stat].value;
        }
        public  void UpdateStatValue(string stat, int change)
        {
            if (!stats.ContainsKey(stat)) return;
            stats[stat].ChangeStat(change);
        }
        public  void AddAchievement(Achievement achievement)
        {
            if(achievements.Contains(achievement)) return;
            achievements.Add(achievement);
            if(!achievement.IsAchieved()) 
            { 
                achievement.Achieved += OnAchievementAchieved;
                achievement.IncrementNotification += OnAchievementIncrementNotification;
            }
        }
        public  void RemoveAchievment(Achievement achievement) 
        { 
            achievements.Remove(achievement);
            achievement.Achieved -= OnAchievementAchieved;
            achievement.IncrementNotification -= OnAchievementIncrementNotification;
        }
        public  void ClearAchievements() { achievements.Clear(); }
        private void OnAchievementAchieved(Achievement achievement)
        {
            achievementDrawStack.Add(new(achievedDisplayDuration, achievement));
            achievement.Achieved -= OnAchievementAchieved;
            achievement.IncrementNotification -= OnAchievementIncrementNotification;
        }
        private void OnAchievementIncrementNotification(Achievement achievement)
        {
            achievementDrawStack.Add(new(notificationDuration, achievement));
        }
    }
}
