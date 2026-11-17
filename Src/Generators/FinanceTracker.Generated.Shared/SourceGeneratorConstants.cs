namespace FinanceTracker.Generated.Shared
{
    /// <summary>
    /// Names the generators and the code they write share.
    /// </summary>
    public static class SourceGeneratorConstants
    {
        /// <summary>
        /// The namespace the generated attributes and enums are in.
        /// </summary>
        public const string GeneratedNamespace = "FinanceTracker.Generated";

        /// <summary>
        /// The attribute registering a class as a scoped service.
        /// </summary>
        public const string ScopedAttributeName = "ScopedAttribute";
        /// <summary>
        /// The attribute registering a class as a transient service.
        /// </summary>
        public const string TransientAttributeName = "TransientAttribute";
        /// <summary>
        /// The attribute registering a class as a singleton service.
        /// </summary>
        public const string SingletonAttributeName = "SingletonAttribute";

        /// <summary>
        /// The attribute saying which registration method and which projects a class is for.
        /// </summary>
        public const string InjectionCategoryAttributeName = "InjectionCategoryAttribute";
        /// <summary>
        /// The enum of registration methods.
        /// </summary>
        public const string InjectionCategoryEnumName = "InjectionCategoryType";

        /// <summary>
        /// The enum of projects a class can be registered in.
        /// </summary>
        public const string ServiceDestinationEnumName = "ServiceDestinationType";

        /// <summary>
        /// The MSBuild property saying which <see cref="ServiceDestinationEnumName"/> a project registers.
        /// </summary>
        public const string ServiceDestinationProperty = "FinTrackServiceDestination";
    }
}
