using Xunit;

namespace Bodyslide.Core.Tests;

[CollectionDefinition("NonParallel", DisableParallelization = true)]
public sealed class NonParallelCollectionDefinition;
