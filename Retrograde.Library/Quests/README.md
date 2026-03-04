# Quests

Everything needed to build and run procedural quest chains.

- **IQuestchain.cs / IOutlawQuest.cs** — Core interfaces for quest chain orchestration and individual quest implementations
- **LoopingQuestChain / RetrogradeQuest / RetrogradeBountyQuest / StaticLayoutQuestChain** — Orchestrators that sequence quest steps and manage chain state
- **MissionTemplate.cs** — Parameter bag passed from template selection through to quest building
- **TemplateLib.cs** — Registry of available mission template libraries
- **Discovery/** — Discovery quest implementations (data-slates, wanted posters)
- **Investigation/** — Investigation quest implementations (activators, destroys, informants, derelicts)
- **Meta/** — Meta-quest logic (forks, branching)
- **Showdown/** — Showdown/bounty quest implementations
- **Templates/** — Static template data tables for each quest category
- **TemplateEngines/** — Template selection logic + `ITemplateEngine` / `ITemplateManager` interfaces
