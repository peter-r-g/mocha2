﻿using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace Mocha.Generators
{
	[Generator( LanguageNames.CSharp )]
	public class ResourceGenerator : IIncrementalGenerator
	{
		private const string ResourceAttributeHint = "ResourceAttribute.g.cs";
		private const string OutputFileHint = "ResourceGen.g.cs";

		private const string ResourceAttribute = "Mocha.ResourceAttribute";
		private const string FileSystemContent = "global::Mocha.FileSystem.Content";
		private const string FileSystemReadAllTextMethod = "ReadAllText";

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(PostInitialize);

			var provider = context.SyntaxProvider.ForAttributeWithMetadataName(ResourceAttribute, SyntaxPredicate, TransformResourceAttribute)
				.Where(obj => obj != default);

			context.RegisterSourceOutput(provider.Collect(), Execute);
		}

		private void PostInitialize(IncrementalGeneratorPostInitializationContext context)
		{
			context.AddSource( ResourceAttributeHint, $$"""
				namespace Mocha;

				/// <summary>
				/// <para>
				/// Specifies that this can be (de-)serialized through JSON.
				/// </para>
				/// <para>
				/// Will codegen a static <c>Load( string filePath )</c> function, which loads using <see cref="{{FileSystemContent}}" />.
				/// </para>
				/// <para>
				/// <b>Note:</b> this structure must be marked as partial.
				/// </para>
				/// </summary>
				[global::System.AttributeUsage( global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false )]
				public class ResourceAttribute : global::System.Attribute
				{
				}
				""");
		}

		private void Execute(SourceProductionContext context, ImmutableArray<ResourceData> resourceData)
		{
			using var output = new StringWriter();
			using var writer = new IndentedTextWriter(output);

			writer.WriteLine("// <auto-generated/>");
			writer.WriteLine();

			var groups = resourceData.GroupBy(resource => resource.ContainingNamespace);

			foreach ( var group in groups )
			{
				var firstEntry = group.First();
				var containingNamespace = firstEntry.ContainingNamespace;

				// Start group namespace
				if ( !string.IsNullOrWhiteSpace( containingNamespace ) )
				{
					writer.WriteLine($"namespace {containingNamespace}");
					writer.WriteLine("{");
					writer.Indent++;
				}

				foreach ( var entry in group )
				{
					// Start entry type
					writer.WriteLine($"partial struct {entry.TypeName}");
					writer.WriteLine("{");
					writer.Indent++;

					// Start Load(string)
					writer.WriteLine("/// <summary>");
					writer.WriteLine("/// <para>");
					writer.WriteLine($"/// Loads a <see cref=\"{entry.TypeName}\" /> structure from a file using <see cref=\"{FileSystemContent}\" />");
					writer.WriteLine("/// </para>");
					writer.WriteLine("/// <para>");
					writer.WriteLine("/// <b>(Auto-generated)</b>");
					writer.WriteLine("/// </para>");
					writer.WriteLine("/// </summary>");

					writer.WriteLine($"public static {entry.TypeName} Load( string filePath )");
					writer.WriteLine("{");
					writer.Indent++;

					writer.WriteLine($"var file = {FileSystemContent}.{FileSystemReadAllTextMethod}( filePath );");
					writer.WriteLine($"return global::System.Text.Json.JsonSerializer.Deserialize<{entry.TypeName}>( file );");

					writer.Indent--;
					writer.WriteLine("}");
					// End Load(string)

					// Start Load(byte[])
					writer.WriteLine("/// <summary>");
					writer.WriteLine("/// <para>");
					writer.WriteLine($"/// Loads a <see cref=\"{entry.TypeName}\" /> structure from a data array.");
					writer.WriteLine("/// </para>");
					writer.WriteLine("/// <para>");
					writer.WriteLine("/// <b>(Auto-generated)</b>");
					writer.WriteLine("/// </para>");
					writer.WriteLine("/// </summary>");

					writer.WriteLine($"public static {entry.TypeName} Load( byte[] data )");
					writer.WriteLine("{");
					writer.Indent++;

					writer.WriteLine($"return global::System.Text.Json.JsonSerializer.Deserialize<{entry.TypeName}>( data );");

					writer.Indent--;
					writer.WriteLine("}");
					// End Load(byte[])

					writer.Indent--;
					writer.WriteLine("}");
					// End entry type
				}

				if (!string.IsNullOrWhiteSpace(containingNamespace))
				{
					writer.Indent--;
					writer.WriteLine("}");
				}
				// End group namespace
			}

			context.CancellationToken.ThrowIfCancellationRequested();
			context.AddSource(OutputFileHint, output.ToString());
		}

		// Will always be true because of the AttributeUsage constraint.
		private bool SyntaxPredicate(SyntaxNode node, CancellationToken token) => true;
		private ResourceData TransformResourceAttribute(GeneratorAttributeSyntaxContext context, CancellationToken token)
		{
			var containingNamespace = context.TargetSymbol.ContainingNamespace.IsGlobalNamespace
				? null
				: context.TargetSymbol.ContainingNamespace.ToDisplayString();
			var typeName = context.TargetSymbol.Name;

			return new ResourceData(containingNamespace, typeName, []);
		}

		private readonly record struct ResourceData
		{
			public readonly string? ContainingNamespace;
			public readonly string TypeName;

			public readonly bool IsError;
			public readonly ImmutableArray<Diagnostic> Diagnostics;

			public ResourceData( string? containingNamespace, string typeName, ImmutableArray<Diagnostic> diagnostics )
			{
				ContainingNamespace = containingNamespace;
				TypeName = typeName;

				IsError = false;
				Diagnostics = diagnostics;
			}

			public ResourceData( ImmutableArray<Diagnostic> diagnostics )
			{
				ContainingNamespace = null;
				TypeName = string.Empty;

				IsError = true;
				Diagnostics = diagnostics;
			}
		}
	}
}
