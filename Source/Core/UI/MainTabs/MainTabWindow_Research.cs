﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace RA
{
    public class MainTabWindow_Research : MainTabWindow
    {
        public Texture2D texSortByCost = ContentFinder<Texture2D>.Get("UI/Buttons/SortByCost");
        public Texture2D texSortByName = ContentFinder<Texture2D>.Get("UI/Buttons/SortByName");

        public static readonly Texture2D BarFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.85f));
        public static readonly Texture2D BarBgTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));

        public const float LeftAreaWidth = 330f;
        public const int ModeSelectButHeight = 40;
        public const float ProjectTitleHeight = 50f;
        public const float ProjectTitleLeftMargin = 20f;
        public const int ProjectIntervalY = 25;

        public enum ShowResearch
        {
            Available,
            Completed,
            All
        }
        public enum SortOptions
        {
            Name,
            Cost
        }

        public ShowResearch showResearchedProjects = ShowResearch.Available;
        public SortOptions currentSortOption = SortOptions.Cost;

        public Vector2 projectListScrollPosition = default(Vector2);

        public IEnumerable<ResearchProjectDef> researchProjectsList;
        public ResearchProjectDef selectedProject = Find.ResearchManager.currentProj;
        
        public bool noBenchWarned;
        public bool sortAscending;
        public string currentFilter = "";
        public string previousFilter = "";

        public override float TabButtonBarPercent
        {
            get
            {
                ResearchProjectDef currentProj = Find.ResearchManager.currentProj;
                if (currentProj == null)
                {
                    return 0f;
                }
                return currentProj.PercentComplete;
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();

            RefreshList();
        }

        public override void DoWindowContents(Rect tabRect)
        {
            base.DoWindowContents(tabRect);

            // throws message if no research bench is build yet
            if (!noBenchWarned)
            {
                if (!Find.ListerBuildings.ColonistsHaveBuilding(ThingDefOf.ResearchBench))
                {
                    Find.WindowStack.Add(new Dialog_Message("ResearchMenuWithoutBench".Translate()));
                }
                noBenchWarned = true;
            }

            // header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(0f, 0f, tabRect.width, 300f), "Research".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect rectSidePanel = new Rect(0f, 75f, 330f, tabRect.height - 75f);
            DrawSidePanel(rectSidePanel);

            Rect rectDetailsPanel = new Rect(rectSidePanel.xMax + 10f, 45f, tabRect.width - rectSidePanel.width - 10f, tabRect.height - 45f);
            DrawDetailsPanel(rectDetailsPanel);

        }

        public void DrawSidePanel(Rect rectSidePanel)
        {
            Widgets.DrawMenuSection(rectSidePanel, false);

            // controls row that adds search textfield and sorter
            Rect rectControlsRow = rectSidePanel.ContractedBy(10f);
            rectControlsRow.height = 30f;

            Rect rectTextFilter = new Rect(rectControlsRow);
            rectTextFilter.width = rectControlsRow.width - 110f;
            Rect rectClearFilterButton = new Rect(rectTextFilter.xMax + 6f, rectTextFilter.yMin + 3f, 24f, 24f);
            Rect rectSortByNameButton = new Rect(rectClearFilterButton.xMax + 6f, rectTextFilter.yMin + 3f, 24f, 24f);
            Rect rectSortByCostButton = new Rect(rectSortByNameButton.xMax + 6f, rectTextFilter.yMin + 3f, 24f, 24f);

            // draw filter text field, return input
            currentFilter = Widgets.TextField(rectTextFilter, currentFilter);
            if (previousFilter != currentFilter)
            {
                previousFilter = currentFilter;
                RefreshList();
            }
            // if any text in the field
            if (currentFilter != "")
            {
                // draw clear filter text field button
                if (Widgets.ImageButton(rectClearFilterButton, Widgets.CheckboxOffTex))
                {
                    currentFilter = "";
                    RefreshList();
                }
            }

            // draw name sorter button
            if (Widgets.ImageButton(rectSortByNameButton, texSortByName))
            {
                // if other sort option was selected before
                if (currentSortOption != SortOptions.Name)
                {
                    // do the ascending sort by cost
                    currentSortOption = SortOptions.Name;
                    sortAscending = true;
                    RefreshList();
                }
                // else inverse the sorting method for the current sort option
                else
                {
                    sortAscending = !sortAscending;
                    RefreshList();
                }
            }
            // draw cost sorter button
            if (Widgets.ImageButton(rectSortByCostButton, texSortByCost))
            {
                if (currentSortOption != SortOptions.Cost)
                {
                    currentSortOption = SortOptions.Cost;
                    sortAscending = true;
                    RefreshList();
                }
                else
                {
                    sortAscending = !sortAscending;
                    RefreshList();
                }
            }

            // research projects list
            Rect rectResearchesList = rectSidePanel.ContractedBy(10f);
            rectResearchesList.yMin += 30f;
            rectResearchesList.height -= 30f;
            float height = 25 * researchProjectsList.Count() + 100;
            Rect sidebarContent = new Rect(0f, 0f, rectResearchesList.width - 16f, height);
            Widgets.BeginScrollView(rectResearchesList, ref projectListScrollPosition, sidebarContent);
            Rect position = sidebarContent.ContractedBy(10f);
            GUI.BeginGroup(position);
            int num = 0;

            foreach (ResearchProjectDef current in researchProjectsList)
            {
                Rect rectCurrentProject = new Rect(0f, num, position.width, 25f);
                if (selectedProject == current)
                {
                    GUI.DrawTexture(rectCurrentProject, TexUI.HighlightTex);
                }

                string text = current.LabelCap + " (" + current.totalCost.ToString("F0") + ")";
                Rect sidebarRowInner = new Rect(rectCurrentProject);
                sidebarRowInner.x += 6f;
                sidebarRowInner.width -= 6f;
                float num2 = Text.CalcHeight(text, sidebarRowInner.width);
                if (sidebarRowInner.height < num2)
                {
                    sidebarRowInner.height = num2 + 3f;
                }

                // give the label a colour if we're in the all tab.
                Color textColor;
                if (showResearchedProjects == ShowResearch.All)
                {
                    if (current.IsFinished)
                    {
                        textColor = new Color(1f, 1f, 1f);
                    }
                    else if (!current.PrereqsFulfilled)
                    {
                        textColor = new Color(.6f, .6f, .6f);
                    }
                    else
                    {
                        textColor = new Color(.8f, .85f, 1f);
                    }
                }
                else
                {
                    textColor = new Color(.8f, .85f, 1f);
                }
                if (Widgets.TextButton(sidebarRowInner, text, false, true, textColor))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    selectedProject = current;
                }
                num += 25;
            }
            Widgets.EndScrollView();
            GUI.EndGroup();

            // draw research tabs selection
            List<TabRecord> list = new List<TabRecord>();
            list.Add(new TabRecord("Available", () =>
            {
                this.showResearchedProjects = ShowResearch.Available;
                RefreshList();
            }, showResearchedProjects == ShowResearch.Available));
            list.Add(new TabRecord("Researched", () =>
            {
                this.showResearchedProjects = ShowResearch.Completed;
                RefreshList();
            }, showResearchedProjects == ShowResearch.Completed));
            if (Prefs.DevMode)
            {
                list.Add(new TabRecord("All", () =>
                {
                    this.showResearchedProjects = ShowResearch.All;
                    RefreshList();
                }, showResearchedProjects == ShowResearch.All));
                TabDrawer.DrawTabs(rectSidePanel, list);
            }
        }

        public void DrawDetailsPanel(Rect rectDetailsPanel)
        {
            Widgets.DrawMenuSection(rectDetailsPanel);

            Rect rectInnerDetailsPanel = rectDetailsPanel.ContractedBy(20f);
            GUI.BeginGroup(rectInnerDetailsPanel);
            if (selectedProject != null)
            {
                // selected project label
                Text.Font = GameFont.Medium;
                GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
                Rect rectResearchLabel = new Rect(20f, 0f, rectInnerDetailsPanel.width - 20f, 50f);
                Widgets.Label(rectResearchLabel, selectedProject.LabelCap);
                GenUI.ResetLabelAlign();
                Text.Font = GameFont.Small;

                // draw project description
                Rect rectDescription = new Rect(0f, 50f, rectInnerDetailsPanel.width, rectInnerDetailsPanel.height - 50f);
                StringBuilder descriptionString = new StringBuilder();
                descriptionString.Append(selectedProject.description + "\n\n");
                RecipeDef researchRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail(selectedProject.defName);
                // if there are required ingridients
                if (researchRecipe != null && !researchRecipe.ingredients.NullOrEmpty())
                {
                    // draw research prerequisites
                    descriptionString.Append("Required research materials:\n");
                    foreach (IngredientCount ingridient in researchRecipe.ingredients)
                    {
                        descriptionString.AppendFormat("\t{0} ", ingridient.GetBaseCount());

                        if (ingridient.GetBaseCount() == 1)
                            descriptionString.Append("sample ");
                        else
                            descriptionString.Append("samples ");

                        if (!ingridient.filter.categories.NullOrEmpty())
                        {
                            descriptionString.AppendFormat("of {0} category\n", ThingCategoryDef.Named(ingridient.filter.categories[0]).LabelCap);
                        }
                        else
                            descriptionString.AppendFormat("{0}\n", ingridient.filter.thingDefs[0].LabelCap);
                    }
                }
                Widgets.Label(rectDescription, descriptionString.ToString());

                Rect rectResearchButton = new Rect(rectInnerDetailsPanel.width / 2f - 50f, 300f, 100f, 50f);
                if (selectedProject.IsFinished)
                {
                    Widgets.DrawMenuSection(rectResearchButton);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rectResearchButton, "Finished".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else if (selectedProject == Find.ResearchManager.currentProj)
                {
                    Widgets.DrawMenuSection(rectResearchButton);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rectResearchButton, "InProgress".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else if (!selectedProject.PrereqsFulfilled)
                {
                    Widgets.DrawMenuSection(rectResearchButton);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rectResearchButton, "Locked");
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else
                {
                    if (Widgets.TextButton(rectResearchButton, "Research".Translate()))
                    {
                        SoundDef.Named("ResearchStart").PlayOneShotOnCamera();
                        Find.ResearchManager.currentProj = selectedProject;
                    }
                    if (Prefs.DevMode)
                    {
                        Rect rectInstaResearchButton = rectResearchButton;
                        rectInstaResearchButton.x += rectInstaResearchButton.width + 4f;
                        if (Widgets.TextButton(rectInstaResearchButton, "Debug Insta-finish"))
                        {
                            Find.ResearchManager.currentProj = selectedProject;
                            Find.ResearchManager.InstantFinish(selectedProject);
                        }
                    }
                }

                Rect rectProgressBar = new Rect(15f, 450f, rectInnerDetailsPanel.width - 30f, 35f);
                Widgets.FillableBar(rectProgressBar, selectedProject.PercentComplete, BarFillTex, BarBgTex, true);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rectProgressBar, selectedProject.ProgressNumbersString);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            GUI.EndGroup();
        }

        // define, which research defs to draw
        public void RefreshList()
        {
            switch (showResearchedProjects)
            {
                case ShowResearch.All:
                    researchProjectsList = DefDatabase<ResearchProjectDef>.AllDefs.Where(projectDef => !projectDef.prerequisites.Contains(projectDef));
                    break;
                case ShowResearch.Completed:
                    researchProjectsList = DefDatabase<ResearchProjectDef>.AllDefs.Where(projectDef => projectDef.IsFinished && projectDef.PrereqsFulfilled);
                    break;
                default:
                    researchProjectsList = DefDatabase<ResearchProjectDef>.AllDefs.Where(projectDef => !projectDef.IsFinished && projectDef.PrereqsFulfilled);
                    break;
            }

            if (currentFilter != "")
            {
                researchProjectsList = researchProjectsList.Where(projectDef => projectDef.label.ToUpper().Contains(currentFilter.ToUpper()));
            }

            switch (currentSortOption)
            {
                case SortOptions.Cost:
                    researchProjectsList = researchProjectsList.OrderBy(projectDef => projectDef.totalCost);
                    break;
                case SortOptions.Name:
                    researchProjectsList = researchProjectsList.OrderBy(projectDef => projectDef.LabelCap);
                    break;
                default:
                    break;
            }

            if (sortAscending) researchProjectsList = researchProjectsList.Reverse();
        }
    }
}