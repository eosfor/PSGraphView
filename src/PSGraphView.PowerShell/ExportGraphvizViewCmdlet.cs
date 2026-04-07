using System.Management.Automation;
using System.Text;
using PSGraphView.Graphviz;

namespace PSGraphView.PowerShell;

[Cmdlet(VerbsData.Export, "GraphvizView", DefaultParameterSetName = InputObjectParameterSet)]
public sealed class ExportGraphvizViewCmdlet : PSCmdlet
{
    private const string InputObjectParameterSet = "InputObject";
    private const string DotPathParameterSet = "DotPath";

    private readonly StringBuilder _dotBuilder = new();

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
    [AllowEmptyString]
    public string InputObject { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = DotPathParameterSet)]
    [ValidateNotNullOrEmpty]
    public string DotPath { get; set; } = null!;

    [Parameter(Mandatory = true)]
    public GraphvizLayoutEngine Renderer { get; set; }

    [Parameter]
    public ViewOutputKind As { get; set; }

    [Parameter]
    public string? OutputPath { get; set; }

    protected override void ProcessRecord()
    {
        if (ParameterSetName != InputObjectParameterSet)
        {
            return;
        }

        _dotBuilder.Append(InputObject);
        if (!InputObject.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            _dotBuilder.AppendLine();
        }
    }

    protected override void EndProcessing()
    {
        try
        {
            var outputKind = CmdletOutputHelpers.ResolveOutputKind(
                MyInvocation.BoundParameters,
                nameof(As),
                As,
                OutputPath,
                ViewOutputKind.Svg);

            CmdletOutputHelpers.ValidateSupportedOutputs(
                this,
                Renderer.ToString(),
                outputKind,
                ViewOutputKind.Json,
                ViewOutputKind.Svg,
                ViewOutputKind.Png,
                ViewOutputKind.Jpg);

            var dot = ParameterSetName switch
            {
                InputObjectParameterSet => _dotBuilder.ToString(),
                DotPathParameterSet => File.ReadAllText(DotPath),
                _ => throw new NotSupportedException($"Parameter set '{ParameterSetName}' is not supported.")
            };

            if (outputKind == ViewOutputKind.Json)
            {
                var xdotJson = GraphvizNativeLayoutRenderer.RenderXdotJson(dot, Renderer);
                CmdletOutputHelpers.WriteResult(this, xdotJson, OutputPath);
                return;
            }

            if (outputKind == ViewOutputKind.Svg)
            {
                var xdotJson = GraphvizNativeLayoutRenderer.RenderXdotJson(dot, Renderer);
                var scene = new GraphvizXdotJsonSceneInterpreter().Interpret(xdotJson);
                var svg = new GraphSceneSvgRenderer().Render(scene);
                CmdletOutputHelpers.WriteResult(this, svg, OutputPath);
                return;
            }

            if (outputKind is ViewOutputKind.Png or ViewOutputKind.Jpg)
            {
                var xdotJson = GraphvizNativeLayoutRenderer.RenderXdotJson(dot, Renderer);
                var scene = new GraphvizXdotJsonSceneInterpreter().Interpret(xdotJson);
                var rasterData = new GraphSceneRasterRenderer().Render(
                    scene,
                    outputKind == ViewOutputKind.Png ? RasterOutputKind.Png : RasterOutputKind.Jpeg);
                CmdletOutputHelpers.WriteResult(this, rasterData, OutputPath);
                return;
            }

            var data = GraphvizProcessRenderer.Render(dot, Renderer, outputKind);

            CmdletOutputHelpers.WriteResult(this, data, OutputPath);
        }
        catch (FileNotFoundException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.GraphvizExecutableNotFound",
                ErrorCategory.ObjectNotFound,
                DotPath));
        }
        catch (DirectoryNotFoundException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.DotPathNotFound",
                ErrorCategory.ObjectNotFound,
                DotPath));
        }
        catch (IOException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.DotInputReadFailed",
                ErrorCategory.ReadError,
                DotPath));
        }
        catch (GraphvizNativeException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.GraphvizNativeLayoutFailed",
                ErrorCategory.InvalidOperation,
                Renderer));
        }
        catch (DllNotFoundException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.GraphvizNativeRuntimeNotFound",
                ErrorCategory.ObjectNotFound,
                Renderer));
        }
        catch (InvalidOperationException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.GraphvizRenderFailed",
                ErrorCategory.InvalidOperation,
                Renderer));
        }
        catch (InvalidDataException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.GraphvizSceneDecodeFailed",
                ErrorCategory.InvalidData,
                Renderer));
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.InvalidDotInput",
                ErrorCategory.InvalidArgument,
                ParameterSetName == DotPathParameterSet ? DotPath : InputObject));
        }
    }
}
