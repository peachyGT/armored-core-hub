param([string]$SourcePng, [string]$OutputPath)
# Run with Windows PowerShell -STA. Converts the checked-in simple vector mark
# (or a local user-supplied PNG) to a Windows icon with nine native size frames.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$projectRoot=Split-Path $PSScriptRoot -Parent
if(!$OutputPath){$OutputPath=Join-Path $projectRoot 'src/Hub.App/Assets/Hub.ico'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
if($SourcePng){
    $source=[Windows.Media.Imaging.BitmapFrame]::Create([Uri][IO.Path]::GetFullPath($SourcePng),[Windows.Media.Imaging.BitmapCreateOptions]::None,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
}else{
    [xml]$vector=Get-Content -LiteralPath (Join-Path $projectRoot 'src/Hub.App/Assets/Hub.svg') -Raw
}
$sizes=@(16,20,24,32,40,48,64,128,256)
$frames=@()
foreach($size in $sizes){
    $visual=New-Object Windows.Media.DrawingVisual
    $drawing=$visual.RenderOpen()
    if($SourcePng){
        $drawing.DrawImage($source,(New-Object Windows.Rect(0,0,$size,$size)))
    }else{
        $drawing.PushTransform((New-Object Windows.Media.ScaleTransform(($size/256.0),($size/256.0))))
        foreach($shape in $vector.svg.path){
            $fill=[Windows.Media.BrushConverter]::new().ConvertFromString($shape.fill)
            $stroke=[Windows.Media.BrushConverter]::new().ConvertFromString($shape.stroke)
            $pen=New-Object Windows.Media.Pen($stroke,([double]$shape.'stroke-width'))
            $drawing.DrawGeometry($fill,$pen,[Windows.Media.Geometry]::Parse($shape.d))
        }
        $drawing.Pop()
    }
    $drawing.Close()
    $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $png=New-Object Windows.Media.Imaging.PngBitmapEncoder
    $png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $buffer=New-Object IO.MemoryStream
    $png.Save($buffer)
    $frames+=,@{Size=$size; Bytes=$buffer.ToArray()}
    $buffer.Dispose()
}
$stream=[IO.File]::Create($OutputPath)
$writer=New-Object IO.BinaryWriter($stream)
try{
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames){
        $dimension=if($frame.Size -eq 256){0}else{$frame.Size}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset+=$frame.Bytes.Length
    }
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
}finally{$writer.Dispose()}
Write-Output "Created $OutputPath ($($frames.Count) sizes, 16-256px)"
