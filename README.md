## TowerOfCode

### What is TowerOfCode?
**TowerOfCode** is a powerful command-line utility designed to help .NET developers safely restructure their projects while maintaining code integrity and preserving dependencies. Inspired by the Tower of Hanoi puzzle, the tool ensures a step-by-step, safe transition of code elements between layers and modules.

### Why use TowerOfCode?
* **Safe Code Migration** - Moves code elements between layers with minimal risk of breaking existing functionality
* **Dependency Management** - Automatically updates all references and namespace usages
* **Step-by-Step Process** - Uses the Tower of Hanoi algorithm for safe multi-step restructuring
* **Fine-Grained Control** - Supports both class-level and project-level migrations
* **Namespace Renaming** - Handles complex namespace changes across multiple projects
* **Rollback Capabilities** - Provides options to revert changes if needed

### How it works
1. **Analysis Phase**
   * Scans project structure
   * Builds dependency graph
   * Identifies safe migration paths

2. **Migration Phase**
   * Moves code to temporary location
   * Updates references and usages
   * Performs final move to target location

3. **Verification Phase**
   * Validates all references
   * Checks namespace consistency
   * Ensures buildability