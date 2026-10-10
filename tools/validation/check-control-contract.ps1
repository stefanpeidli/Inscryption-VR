param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[void][Reflection.Assembly]::LoadFrom((Join-Path $root '.dependencies\BepInEx\BepInEx\core\Mono.Cecil.dll'))
$managed = Join-Path $GamePath 'Inscryption_Data\Managed'
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'), $parameters)
$contracts = @(
    @('DiskCardGame.InteractionCursor','Fields','currentMainInputInteractable,cursorDownInteractable,excludedLayers'),
    @('DiskCardGame.InteractionCursor','Methods','InteractionDisabledWith'),
    @('DiskCardGame.MainInputInteractable','Properties','Enabled'),
    @('DiskCardGame.MainInputInteractable','Methods','CursorEnter,CursorStay,CursorExit,CursorSelectStart,CursorSelectEnd,CursorDragOff'),
    @('DiskCardGame.ViewManager','Properties','CurrentView'),
    @('PauseMenu','Fields','instance'),
    @('PauseMenu','Properties','Paused,Unpausing,ActivePauseCursor'),
    @('DiskCardGame.Part1GameFlowManager','Properties','CurrentGameState,Transitioning'),
    @('DiskCardGame.TurnManager','Properties','IsMainOrDrawPhaseNotUpkeep,IsSetupPhase,GameEnding,GameEnded'),
    @('DiskCardGame.GameMap','Properties','FullyUnrolled'),
    @('DiskCardGame.MapNodeManager','Properties','MovingNodes'),
    @('DiskCardGame.ViewController','Fields','allowedViews,altTransitionInputs'),
    @('DiskCardGame.ViewController','Properties','LockState'),
    @('DiskCardGame.ViewController/ViewTransitionInput','Fields','from,to,input'),
    @('DiskCardGame.ViewManager','Properties','Controller'),
    @('DiskCardGame.ViewManager','Methods','SwitchToView'),
    @('DiskCardGame.RuleBookController','Properties','Shown'),
    @('DiskCardGame.RuleBookController','Methods','SetShown'),
    @('DiskCardGame.TextDisplayer','Fields','textMesh,continuePressed'),
    @('DiskCardGame.TextDisplayer','Properties','Displaying,CurrentAdvanceMode'),
    @('DiskCardGame.SpecialNodeHandler','Fields','cardChoiceSequencer,cardMerger'),
    @('DiskCardGame.CardChoicesSequencer','Fields','selectableCards'),
    @('DiskCardGame.CardSingleChoicesSequencer','Fields','rerollInteractable'),
    @('DiskCardGame.CardMergeSequencer','Fields','sacrificeSlot,hostSlot,confirmStone,stoneCircleAnim'),
    @('DiskCardGame.SelectCardFromDeckSlot','Fields','cardSelector'),
    @('DiskCardGame.SelectCardFromDeckSlot','Properties','Card'),
    @('DiskCardGame.SelectableCardArray','Fields','displayedCards')
)
try {
    $checks = 0
    foreach ($contract in $contracts) {
        foreach ($name in $contract[2].Split(',')) {
            $type = $assembly.MainModule.GetType($contract[0])
            if (!$type) { throw ('Missing type: ' + $contract[0]) }
            $found = $false
            while ($type) {
                if (@($type.($contract[1]) | Where-Object Name -eq $name).Count) { $found = $true; break }
                $type = if ($type.BaseType) { $type.BaseType.Resolve() } else { $null }
            }
            if (!$found) { throw ('Missing member: ' + $contract[0] + '.' + $name) }
            $checks++
        }
    }
    Write-Output "PASS: $checks installed-game controller contract members verified (metadata only)."
} finally { $assembly.Dispose(); $resolver.Dispose() }
