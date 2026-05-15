using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class StoryStateEvaluatorTests
{
    public static void RunAll()
    {
        Evaluate_FiveNodes_ProducesExpectedCountsAndOrder();
        Evaluate_NodeWithUnknownPatchWhen_IsUnknownNotCurrent();
        Evaluate_NodeWithRelationshipPatchWhen_Failed_IsLocked();
        Evaluate_NodeWithRelationshipPatchWhen_Negated_UsesEngagedAlias();
        Evaluate_NodeWithConversationTopicPatchWhen_Passed_IsCurrent();
        Evaluate_NodeWithHasFlagPatchWhen_Passed_IsCurrent();
        Evaluate_NodeWithHasSeenEventValuePatchWhen_Passed_IsCurrent();
        Evaluate_NodeWithSpouseContainsPatchWhen_Passed_IsCurrent();
        Evaluate_UnknownConditionReport_AggregatesRawAndCounts();
        Evaluate_NodeWithHeartsPatchWhen_Failed_IsLocked();
        Evaluate_NodeWithFarmerCheaterPatchWhen_Passed_IsCurrent();
        Evaluate_NonNumericEventIdWithoutPreconditions_IsUnknownNotCurrent();
        Evaluate_ConfigSplashArt_Yes_FromSourceModConfig();
        Evaluate_ConfigSamCustomSprites_Yes_FromSourceModConfig();
        Evaluate_DynamicTokenSebGameStatus_AllGood_WhenBranchPasses();
        Evaluate_Query_HasModOrSamCustomSprites_PassesWhenConfigYes();
        Evaluate_Query_HasModFalseOrDanceSpritesYes_PassesWhenDanceSpritesYes();
        Evaluate_DayEventWedding_WithDayEventsUnknown_IsRuntimeMissing();
        Evaluate_DayEventWedding_WithKnownEmptyDayEvents_Fails();
        Evaluate_DayEventWedding_WithKnownWedding_Passes();
        Evaluate_ComplexQuery_RemainsUnknown();
        Evaluate_Query_WithQueryPrefixInKey_PassesWhenConfigYes();
        Evaluate_DynamicTokenSebGameStatus_AllGood_WithQueryGuard();
        Evaluate_DynamicTokenSebbySprite_Yes_WithQueryGuard();
        Evaluate_ActiveDialogueEvent_Alias_NotUnsupported();
        Evaluate_Pregnant_When_IsRuntimeMissingNotParseUnknown();
        Evaluate_NonNumericBranchTargets_AreNotUnknown();
    }

    private static void Evaluate_NonNumericEventIdWithoutPreconditions_IsUnknownNotCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "PlayerKilled",
            sourceModId: "Tests.SpecialTrigger",
            sourceModName: "Special Trigger Pack",
            location: "Town",
            conditionAst: new ConditionAstNode { Type = "AllOf" },
            eventKind: StoryNodeEventKind.SpecialGameEvent);

        var report = evaluator.Evaluate(new[] { node }, state);
        var evaluation = report.Nodes.Single();

        AssertEqual(StoryNodeStatus.SpecialEvent, evaluation.Status, "Special game event ids must not be classified as Unknown.");
        AssertTrue(
            evaluation.StatusReason.Contains("game-triggered", StringComparison.OrdinalIgnoreCase) ||
            evaluation.StatusReason.Contains("special", StringComparison.OrdinalIgnoreCase) ||
            evaluation.StatusReason.Contains("non-numeric", StringComparison.OrdinalIgnoreCase),
            "Status reason should explain the entry is a special / non-numeric trigger."
        );
    }

    private static void Evaluate_FiveNodes_ProducesExpectedCountsAndOrder()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var nodes = new[]
        {
            CreateTriggeredNode(),
            CreateCurrentNode(),
            CreateAvailableLaterNode(),
            CreateLockedNode(),
            CreateUnknownNode()
        };

        var report = evaluator.Evaluate(nodes, state);

        AssertEqual(5, report.TotalNodeCount, "TotalNodeCount mismatch.");
        AssertEqual(1, report.StatusCounts["Triggered"], "Triggered count mismatch.");
        AssertEqual(1, report.StatusCounts["Current"], "Current count mismatch.");
        AssertEqual(1, report.StatusCounts["AvailableLater"], "AvailableLater count mismatch.");
        AssertEqual(1, report.StatusCounts["Locked"], "Locked count mismatch.");
        AssertEqual(1, report.StatusCounts["Unknown"], "Unknown count mismatch.");

        AssertEqual("200001", report.Nodes[0].EventId, "Current node should sort first.");
        AssertEqual(StoryNodeStatus.Current, report.Nodes[0].Status, "First node should be Current.");

        AssertEqual("200002", report.Nodes[1].EventId, "AvailableLater node should sort second.");
        AssertEqual(StoryNodeStatus.AvailableLater, report.Nodes[1].Status, "Second node should be AvailableLater.");

        AssertEqual("200003", report.Nodes[2].EventId, "Locked node should sort third.");
        AssertEqual(StoryNodeStatus.Locked, report.Nodes[2].Status, "Third node should be Locked.");

        AssertEqual("200004", report.Nodes[3].EventId, "Unknown node should sort fourth.");
        AssertEqual(StoryNodeStatus.Unknown, report.Nodes[3].Status, "Fourth node should be Unknown.");

        AssertEqual("100001", report.Nodes[4].EventId, "Triggered node should sort last.");
        AssertEqual(StoryNodeStatus.Triggered, report.Nodes[4].Status, "Last node should be Triggered.");
    }

    private static void Evaluate_NodeWithUnknownPatchWhen_IsUnknownNotCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "300001",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Time", "t 600 2400", "600", "2400")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "CustomToken:Alex",
            Value = "Engaged",
            IsKnown = false,
            Reason = "Patch-level When condition is not evaluated."
        });

        var report = evaluator.Evaluate(new[] { node }, state);

        AssertEqual(StoryNodeStatus.Unknown, report.Nodes.Single().Status, "Unknown CP When should prevent Current status.");
        AssertTrue(
            report.Nodes.Single().StatusReason.Contains("CustomToken:Alex", StringComparison.Ordinal),
            "Status reason should mention the unknown CP When condition."
        );
    }

    private static void Evaluate_NodeWithRelationshipPatchWhen_Failed_IsLocked()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "300002",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Dating", "D Sebastian", "Sebastian")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Relationship:Sebastian |contains=Engaged",
            Value = "true",
            RawValue = "true",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var report = evaluator.Evaluate(new[] { node }, state);
        var evaluation = report.Nodes.Single();

        AssertEqual(StoryNodeStatus.Locked, evaluation.Status, "Known-failed relationship CP When should lock the node.");
        AssertTrue(
            evaluation.PatchWhenConditions.Single().IsKnown && evaluation.PatchWhenConditions.Single().Passed == false,
            "Relationship CP When should be evaluated and marked failed."
        );
        AssertTrue(
            evaluation.StatusReason.Contains("Patch-level progression conditions failed", StringComparison.Ordinal),
            "Status reason should mention patch-level progression failure."
        );
    }

    private static void Evaluate_NodeWithRelationshipPatchWhen_Negated_UsesEngagedAlias()
    {
        var evaluator = new StoryStateEvaluator();
        var notEngagedState = CreateBaseState();
        var engagedState = CreateBaseState(engagedTo: "Sebastian");
        var node = CreateNode(
            eventId: "300005",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Relationship:Sebastian |contains=Engaged",
            Value = "false",
            RawValue = "false",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var notEngagedEvaluation = evaluator.Evaluate(new[] { node }, notEngagedState).Nodes.Single();
        AssertEqual(StoryNodeStatus.Current, notEngagedEvaluation.Status, "Relationship contains=false should pass when the player is not engaged.");
        AssertTrue(
            notEngagedEvaluation.PatchWhenConditions.Single().Passed == true,
            "Relationship contains=false should evaluate to passed when not engaged."
        );

        var engagedEvaluation = evaluator.Evaluate(new[] { node }, engagedState).Nodes.Single();
        AssertEqual(StoryNodeStatus.Locked, engagedEvaluation.Status, "Relationship contains=false should fail when engaged.");
        AssertTrue(
            engagedEvaluation.PatchWhenConditions.Single().Passed == false,
            "Relationship contains=false should evaluate to failed when engaged."
        );
    }

    private static void Evaluate_NodeWithConversationTopicPatchWhen_Passed_IsCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState(dialogueAnswers: new[] { "MaggSamWedding" });
        var node = CreateNode(
            eventId: "300006",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "HasConversationTopic |contains=MaggSamWedding",
            Value = "true",
            RawValue = "true",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var evaluation = evaluator.Evaluate(new[] { node }, state).Nodes.Single();

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "Resolved conversation-topic CP When should not force Unknown.");
        AssertTrue(evaluation.PatchWhenConditions.Single().IsKnown, "HasConversationTopic should be marked known.");
    }

    private static void Evaluate_NodeWithHasFlagPatchWhen_Passed_IsCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState(mail: new[] { "MaggSamFlag" });
        var node = CreateNode(
            eventId: "300007",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "HasFlag",
            Value = "MaggSamFlag",
            RawValue = "\"MaggSamFlag\"",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var evaluation = evaluator.Evaluate(new[] { node }, state).Nodes.Single();

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "Resolved HasFlag CP When should not force Unknown.");
        AssertTrue(evaluation.PatchWhenConditions.Single().IsKnown, "HasFlag should be marked known.");
    }

    private static void Evaluate_NodeWithSpouseContainsPatchWhen_Passed_IsCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState(spouse: "Wizard");
        var node = CreateNode(
            eventId: "300008",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Spouse |contains=Wizard",
            Value = "true",
            RawValue = "true",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var evaluation = evaluator.Evaluate(new[] { node }, state).Nodes.Single();

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "Resolved Spouse contains CP When should not force Unknown.");
        AssertTrue(evaluation.PatchWhenConditions.Single().IsKnown, "Spouse contains should be marked known.");
    }

    private static void Evaluate_NodeWithHasSeenEventValuePatchWhen_Passed_IsCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState(seenEvents: new[] { "502261" });
        var node = CreateNode(
            eventId: "300009",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "HasSeenEvent",
            Value = "502261",
            RawValue = "\"502261\"",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var evaluation = evaluator.Evaluate(new[] { node }, state).Nodes.Single();

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "HasSeenEvent value-form CP When should not force Unknown.");
        AssertTrue(evaluation.PatchWhenConditions.Single().IsKnown, "HasSeenEvent value-form should be marked known.");
    }

    private static void Evaluate_UnknownConditionReport_AggregatesRawAndCounts()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var first = CreateNode(
            eventId: "390001",
            sourceModId: "Tests.Unknowns",
            sourceModName: "Unknown Pack",
            location: "Town",
            conditionAst: CreateAtom("Unknown", "CustomThing Alpha")
        );
        first.UnknownFragments.Add("CustomThing Alpha");
        first.EvidenceRefs.Add(new EvidenceRef { SourcePath = "content.json", JsonPath = "$.Changes[0]" });
        var second = CreateNode(
            eventId: "390002",
            sourceModId: "Tests.Unknowns",
            sourceModName: "Unknown Pack",
            location: "Town",
            conditionAst: CreateAtom("Unknown", "CustomThing Alpha")
        );
        second.UnknownFragments.Add("CustomThing Alpha");
        second.EvidenceRefs.Add(new EvidenceRef { SourcePath = "events.json", JsonPath = "$.foo" });

        var report = evaluator.Evaluate(new[] { first, second }, state);
        var unknown = report.UnknownConditions.Single(entry => entry.Raw == "CustomThing Alpha");

        AssertEqual(2, unknown.Count, "Unknown report should aggregate duplicate raw conditions.");
        AssertTrue(unknown.SourceFiles.Contains("content.json"), "Unknown report should keep source files.");
        AssertTrue(unknown.SourceFiles.Contains("events.json"), "Unknown report should keep source files.");
        AssertTrue(!string.IsNullOrWhiteSpace(unknown.SuggestedParserType), "Unknown report should include a parser hint.");
    }

    private static void Evaluate_NodeWithHeartsPatchWhen_Failed_IsLocked()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "300003",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Hearts:Victor",
            Value = "10",
            RawValue = "\"10\"",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var report = evaluator.Evaluate(new[] { node }, state);
        var evaluation = report.Nodes.Single();

        AssertEqual(StoryNodeStatus.Locked, evaluation.Status, "Known-failed hearts CP When should lock the node.");
        AssertTrue(
            evaluation.PatchWhenConditions.Single().IsKnown && evaluation.PatchWhenConditions.Single().Passed == false,
            "Hearts CP When should be evaluated and marked failed."
        );
    }

    private static void Evaluate_NodeWithFarmerCheaterPatchWhen_Passed_IsCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState(installedModIds: new[] { "Pathoschild.ContentPatcher" });
        var node = CreateNode(
            eventId: "300004",
            sourceModId: "maggplays.SamSpicyExpansion",
            sourceModName: "Maggs Immersive Sam Spicy Expansion",
            location: "Town",
            conditionAst: CreateAtom("Season", "s fall", "fall")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "FarmerCheater",
            Value = "no",
            RawValue = "\"no\"",
            IsKnown = false,
            Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
        });

        var report = evaluator.Evaluate(new[] { node }, state);
        var evaluation = report.Nodes.Single();

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "Resolved FarmerCheater=no should no longer force Unknown.");
        AssertTrue(
            evaluation.PatchWhenConditions.Single().IsKnown && evaluation.PatchWhenConditions.Single().Passed == true,
            "FarmerCheater should be evaluated and marked passed."
        );
    }

    private static StoryNode CreateTriggeredNode()
    {
        return CreateNode(
            eventId: "100001",
            sourceModId: "Tests.Triggered",
            sourceModName: "Triggered Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "Season fall", "fall")
        );
    }

    private static StoryNode CreateCurrentNode()
    {
        return CreateNode(
            eventId: "200001",
            sourceModId: "Tests.Current",
            sourceModName: "Alpha Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "AllOf",
                Children =
                {
                    CreateAtom("Friendship", "f Shane 2000", "Shane", "2000"),
                    CreateAtom("Season", "Season fall", "fall")
                }
            }
        );
    }

    private static StoryNode CreateAvailableLaterNode()
    {
        return CreateNode(
            eventId: "200002",
            sourceModId: "Tests.AvailableLater",
            sourceModName: "Beta Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "AllOf",
                Children =
                {
                    CreateAtom("Friendship", "f Shane 2000", "Shane", "2000"),
                    CreateAtom("Time", "t 600 1200", "600", "1200")
                }
            }
        );
    }

    private static StoryNode CreateLockedNode()
    {
        return CreateNode(
            eventId: "200003",
            sourceModId: "Tests.Locked",
            sourceModName: "Gamma Pack",
            location: "Town",
            conditionAst: CreateAtom("Friendship", "f Shane 3000", "Shane", "3000")
        );
    }

    private static StoryNode CreateUnknownNode()
    {
        return CreateNode(
            eventId: "200004",
            sourceModId: "Tests.Unknown",
            sourceModName: "Omega Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "Unknown",
                Raw = "mystery condition"
            },
            unknownFragments: new[] { "mystery condition" }
        );
    }

    private static void Evaluate_ConfigSplashArt_Yes_FromSourceModConfig()
    {
        var node = CreateNode("400001", "Tests.Config", "Config Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "SplashArt", Value = "yes" });
        node.SourceModConfigValues["SplashArt"] = "yes";

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "SplashArt config patch When should be known.");
        AssertEqual(true, patch.Passed, "SplashArt=yes should pass when config is yes.");
    }

    private static void Evaluate_ConfigSamCustomSprites_Yes_FromSourceModConfig()
    {
        var node = CreateNode("400002", "Tests.Config", "Config Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "SamCustomSprites", Value = "yes" });
        node.SourceModConfigValues["SamCustomSprites"] = "yes";

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "SamCustomSprites config patch When should be known.");
        AssertEqual(true, patch.Passed, "SamCustomSprites=yes should pass when config is yes.");
    }

    private static void Evaluate_DynamicTokenSebGameStatus_AllGood_WhenBranchPasses()
    {
        Evaluate_DynamicTokenSebGameStatus_AllGood_WithQueryGuard();
    }

    private static void Evaluate_DynamicTokenSebGameStatus_AllGood_WithQueryGuard()
    {
        var node = CreateNode("400003", "Tests.Dynamic", "Dynamic Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "SebGameStatus", Value = "allgood" });
        node.SourceModDynamicTokens["SebGameStatus"] = new List<DynamicTokenDefinition>
        {
            new()
            {
                Name = "SebGameStatus",
                Value = "allgood",
                WhenConditions = new List<PatchWhenCondition>
                {
                    new()
                    {
                        Key = "Query: '{{HasSeenEvent |contains=MaggSebGame107092025}}' = 'false' OR '{{HasSeenEvent |contains=MaggSebGame407092025}}' = 'true'",
                        Value = "true"
                    }
                }
            }
        };

        var state = CreateBaseState(seenEvents: new[] { "MaggSebGame407092025" });
        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, state).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "SebGameStatus dynamic token patch When should be known.");
        AssertEqual(true, patch.Passed, "SebGameStatus should resolve to allgood when query guard passes.");
    }

    private static void Evaluate_DynamicTokenSebbySprite_Yes_WithQueryGuard()
    {
        var node = CreateNode("400010", "Tests.Dynamic", "Dynamic Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "SebbySprite", Value = "yes" });
        node.SourceModConfigValues["SebCustomSprites"] = "yes";
        node.SourceModDynamicTokens["SebbySprite"] = new List<DynamicTokenDefinition>
        {
            new()
            {
                Name = "SebbySprite",
                Value = "yes",
                WhenConditions = new List<PatchWhenCondition>
                {
                    new()
                    {
                        Key = "Query: '{{HasMod |contains=maggplays.sebastiansprites,maggplays.SOspritespatchSeb,DSV.Core}}' = 'true' OR '{{SebCustomSprites}}' = 'yes'",
                        Value = "true"
                    }
                }
            }
        };

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "SebbySprite dynamic token patch When should be known.");
        AssertEqual(true, patch.Passed, "SebbySprite should resolve to yes when config/query guard passes.");
    }

    private static void Evaluate_Query_WithQueryPrefixInKey_PassesWhenConfigYes()
    {
        var node = CreateNode("400011", "Tests.Query", "Query Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Query: '{{HasMod |contains=maggplays.samsprites,maggplays.SOspritespatchSeb,DSV.Core}}' = 'true' OR '{{SamCustomSprites}}' = 'yes'",
            Value = "true"
        });
        node.SourceModConfigValues["SamCustomSprites"] = "yes";

        var patch = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single().PatchWhenConditions.Single();
        AssertTrue(patch.IsKnown, "Query stored in When key must normalize and evaluate.");
        AssertEqual(true, patch.Passed, "SamCustomSprites=yes should satisfy the OR query.");
    }

    private static void Evaluate_Query_HasModOrSamCustomSprites_PassesWhenConfigYes()
    {
        var node = CreateNode("400004", "Tests.Query", "Query Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Query",
            Value = "'{{HasMod |contains=maggplays.samsprites,maggplays.SOspritespatchSeb,DSV.Core}}' = 'true' OR '{{SamCustomSprites}}' = 'yes'"
        });
        node.SourceModConfigValues["SamCustomSprites"] = "yes";

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "Simple OR Query should be known.");
        AssertEqual(true, patch.Passed, "SamCustomSprites=yes should satisfy the OR query.");
    }

    private static void Evaluate_Query_HasModFalseOrDanceSpritesYes_PassesWhenDanceSpritesYes()
    {
        var node = CreateNode("400005", "Tests.Query", "Query Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Query",
            Value = "'{{HasMod |contains= DSV.Core,Poltergeister.SeasonalCuteCharacters}}' = 'false' OR '{{DanceSprites}}' = 'yes'"
        });
        node.SourceModConfigValues["DanceSprites"] = "yes";

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertTrue(patch.IsKnown, "HasMod false OR DanceSprites query should be known.");
        AssertEqual(true, patch.Passed, "DanceSprites=yes should satisfy the OR query.");
    }

    private static void Evaluate_DayEventWedding_WithDayEventsUnknown_IsRuntimeMissing()
    {
        var node = CreateNode("400006", "Tests.DayEvent", "DayEvent Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "DayEvent", Value = "wedding" });

        var state = CreateBaseState(dayEventsKnown: false);

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, state).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertEqual(false, patch.IsKnown, "Missing DayEvent runtime export must stay unknown.");
        AssertEqual("runtimeMissing", patch.UnknownKind, "DayEvent should be runtimeMissing.");
        AssertContains(patch.ReasonZh, "未导出 DayEvent", "DayEvent missing-runtime reason mismatch.");
    }

    private static void Evaluate_DayEventWedding_WithKnownEmptyDayEvents_Fails()
    {
        var node = CreateNode("400008", "Tests.DayEvent", "DayEvent Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "DayEvent", Value = "wedding" });

        var state = CreateBaseState(dayEventsKnown: true, dayEvents: Array.Empty<string>());

        var patch = new StoryStateEvaluator().Evaluate(new[] { node }, state).Nodes.Single().PatchWhenConditions.Single();
        AssertTrue(patch.IsKnown, "Known empty DayEvents should still be evaluable.");
        AssertEqual(false, patch.Passed, "Known empty DayEvents should fail wedding check.");
    }

    private static void Evaluate_DayEventWedding_WithKnownWedding_Passes()
    {
        var node = CreateNode("400009", "Tests.DayEvent", "DayEvent Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "DayEvent", Value = "wedding" });

        var state = CreateBaseState(dayEventsKnown: true, dayEvents: new[] { "wedding" });

        var patch = new StoryStateEvaluator().Evaluate(new[] { node }, state).Nodes.Single().PatchWhenConditions.Single();
        AssertTrue(patch.IsKnown, "Known wedding DayEvents should be evaluable.");
        AssertEqual(true, patch.Passed, "Known wedding DayEvents should pass.");
    }

    private static void Evaluate_ActiveDialogueEvent_Alias_NotUnsupported()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "A MaggBatsChickensEvent" });
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        var result = evaluator.Evaluate(parsed.ConditionAst, state);

        AssertEqual(false, result.HasUnknown, "A alias should evaluate without unsupported atoms.");
        AssertEqual(true, result.Passed, "A topic should pass when topic is not active/recorded.");
    }

    private static void Evaluate_Pregnant_When_IsRuntimeMissingNotParseUnknown()
    {
        var node = CreateNode("400012", "Tests.Family", "Family Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition { Key = "Pregnant", Value = "@{{playerName}}" });

        var patch = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single().PatchWhenConditions.Single();
        AssertEqual(false, patch.IsKnown, "Pregnant should stay unknown until runtime export exists.");
        AssertEqual("runtimeMissing", patch.UnknownKind, "Pregnant should be runtimeMissing.");
        AssertEqual("cpFamilyState", patch.ParsedType, "Pregnant parsed type mismatch.");
    }

    private static void Evaluate_NonNumericBranchTargets_AreNotUnknown()
    {
        foreach (var (eventId, kind, status) in new[]
        {
            ("end", StoryNodeEventKind.BranchTarget, StoryNodeStatus.BranchTarget),
            ("healer", StoryNodeEventKind.BranchTarget, StoryNodeStatus.BranchTarget),
            ("MaggHealer", StoryNodeEventKind.SpecialGameEvent, StoryNodeStatus.SpecialEvent),
            ("MaggMage", StoryNodeEventKind.SpecialGameEvent, StoryNodeStatus.SpecialEvent)
        })
        {
            var node = CreateNode(eventId, "Tests.Branch", "Branch Pack", "Town", new ConditionAstNode { Type = "AllOf" }, eventKind: kind);
            var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
            AssertEqual(status, evaluation.Status, $"{eventId} status mismatch.");
            AssertTrue(evaluation.Status != StoryNodeStatus.Unknown, $"{eventId} must not count as Unknown.");
        }
    }

    private static void Evaluate_ComplexQuery_RemainsUnknown()
    {
        var node = CreateNode("400007", "Tests.Query", "Query Pack", "Town", new ConditionAstNode { Type = "AllOf" });
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Query",
            Value = "({{Season}} = 'fall' AND {{Hearts:Shane}} >= 8)"
        });

        var evaluation = new StoryStateEvaluator().Evaluate(new[] { node }, CreateBaseState()).Nodes.Single();
        var patch = evaluation.PatchWhenConditions.Single();

        AssertEqual(false, patch.IsKnown, "Unsupported complex query must remain unknown.");
        AssertEqual("complexQueryUnsupported", patch.UnknownKind, "Complex query unknown kind mismatch.");
        AssertContains(patch.ReasonZh, "复杂 CP Query", "Complex query reason mismatch.");
    }

    private static StoryNode CreateNode(
        string eventId,
        string sourceModId,
        string sourceModName,
        string location,
        ConditionAstNode conditionAst,
        IEnumerable<string>? unknownFragments = null,
        StoryNodeEventKind eventKind = StoryNodeEventKind.RegularLocationEvent)
    {
        return new StoryNode
        {
            NodeId = $"story-node:{eventId}",
            EventId = eventId,
            EventKind = eventKind,
            SourceModId = sourceModId,
            SourceModName = sourceModName,
            AssetTarget = $"Data/Events/{location}",
            Location = location,
            RawKey = eventId,
            ConditionAst = conditionAst,
            UnknownFragments = unknownFragments?.ToList() ?? new List<string>(),
            EvidenceRefs =
            {
                new EvidenceRef
                {
                    Kind = "test",
                    SourcePath = "test.json",
                    JsonPath = "$.nodes[0]"
                }
            }
        };
    }

    private static RuntimeGameState CreateBaseState(
        IEnumerable<string>? installedModIds = null,
        string? engagedTo = null,
        string? spouse = null,
        IEnumerable<string>? mail = null,
        IEnumerable<string>? dialogueAnswers = null,
        IEnumerable<string>? seenEvents = null,
        bool dayEventsKnown = false,
        IEnumerable<string>? dayEvents = null)
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            DayOfWeek = "Friday",
            Time = 1900,
            Weather = "sunny",
            DayEventsKnown = dayEventsKnown,
            DayEvents = dayEvents?.ToList() ?? new List<string>(),
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            InstalledModIds = new HashSet<string>(installedModIds ?? Array.Empty<string>(), StringComparer.Ordinal),
            FriendshipPoints = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Shane"] = 2200,
                ["Sam"] = 1000,
                ["Sebastian"] = 2250,
                ["Victor"] = 2000
            },
            DatingNpcNames = new HashSet<string>(new[] { "Sebastian" }, StringComparer.Ordinal),
            EngagedTo = engagedTo,
            SpouseName = spouse,
            Spouse = spouse,
            SeenEvents = new HashSet<string>(seenEvents ?? new[] { "100001" }, StringComparer.Ordinal),
            Mail = new HashSet<string>(mail ?? new[] { "someMail" }, StringComparer.Ordinal),
            DialogueAnswers = new HashSet<string>(dialogueAnswers ?? new[] { "ShaneAnswerA" }, StringComparer.Ordinal)
        };
    }

    private static ConditionAstNode CreateAtom(string atomType, string raw, params string[] values)
    {
        return new ConditionAstNode
        {
            Type = "Atom",
            AtomType = atomType,
            Raw = raw,
            Values = values.ToList()
        };
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string actual, string expectedSubstring, string message)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected substring: {expectedSubstring}; Actual: {actual}");
        }
    }
}
