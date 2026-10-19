<#
.SYNOPSIS
    Erzeugt src/GodSaveTheScreen.Core/Emoji/emoji-data.json.gz (Emoji-Katalog für M13).

.DESCRIPTION
    Quellen: Unicode emoji-test.txt (Liste, Reihenfolge, Gruppen) und Unicode-CLDR-JSON
    (annotations + annotationsDerived für de/en: Namen und Stichwörter). Optional ergänzt
    eine CSV (Unicodefinder/symbols.csv) deutsche Synonyme.
    Das Ergebnis wird eingecheckt; der Build lädt nichts herunter.

    Nicht übernommen: component, minimally-qualified, unqualified, Länderflaggen
    (Regional-Indicator-Paare), Unterregionsflaggen (Tag-Sequenzen), gemischte Hauttöne.

.PARAMETER SymbolsCsv
    Pfad zu symbols.csv (optional). Spalten: codepoint;name;category;char;synonyms (Trenner ";", Synonyme durch "," getrennt,
    Codepunkt "U+1F600"). Zuordnung über den Basis-Codepunkt ohne U+FE0F.

.PARAMETER CacheDirectory
    Verzeichnis für die heruntergeladenen Quelldateien (Standard: temporäres Verzeichnis).
#>
[CmdletBinding()]
param(
    [string] $SymbolsCsv,
    [string] $CacheDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'gsts-emoji-sources'),
    [string] $Output = (Join-Path $PSScriptRoot '..\src\GodSaveTheScreen.Core\Emoji\emoji-data.json.gz'),
    [string] $UnicodeBase = 'https://unicode.org/Public/emoji/latest',
    [string] $CldrBase = 'https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json'
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null

function Get-Source([string] $Url, [string] $Name) {
    $path = Join-Path $CacheDirectory $Name
    if (-not (Test-Path $path)) {
        Write-Host "Lade $Url"
        Invoke-WebRequest -Uri $Url -OutFile $path
    }
    return $path
}

function Remove-Fe0f([string] $Text) { return $Text.Replace([string][char]0xFE0F, '') }

function Get-CodePoints([string] $Text) {
    $result = [System.Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $cp = [char]::ConvertToUtf32($Text, $i)
        if ([char]::IsHighSurrogate($Text[$i])) { $i++ }
        $result.Add($cp)
    }
    return $result
}

function Read-Annotations([string] $Path, [string] $Root) {
    $json = Get-Content -Raw -Encoding UTF8 $Path | ConvertFrom-Json
    $map = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($property in $json.$Root.annotations.PSObject.Properties) {
        $map[$property.Name] = $property.Value
    }
    return $map
}

$testPath = Get-Source "$UnicodeBase/emoji-test.txt" 'emoji-test.txt'
$sources = [ordered]@{}
$cldr = @{}
foreach ($lang in 'de', 'en') {
    $cldr[$lang] = @(
        (Read-Annotations (Get-Source "$CldrBase/cldr-annotations-full/annotations/$lang/annotations.json" "annotations-$lang.json") 'annotations'),
        (Read-Annotations (Get-Source "$CldrBase/cldr-annotations-derived-full/annotationsDerived/$lang/annotations.json" "annotationsDerived-$lang.json") 'annotationsDerived'))
}

function Find-Annotation([string] $Lang, [string] $Sequence) {
    foreach ($map in $cldr[$Lang]) {
        foreach ($candidate in $Sequence, (Remove-Fe0f $Sequence), ((Remove-Fe0f $Sequence) + [char]0xFE0F)) {
            if ($map.ContainsKey($candidate)) { return $map[$candidate] }
        }
    }
    return $null
}

# Synonyme aus symbols.csv
$synonyms = @{}
if ($SymbolsCsv) {
    # Format: codepoint;name;category;char;synonyms  (Synonyme durch Komma getrennt, Codepunkt als U+XXXX)
    # Zeilen selbst zerlegen (Import-Csv scheitert an Anführungszeichen in der Spalte "char").
    foreach ($line in Get-Content -Encoding UTF8 -Path $SymbolsCsv) {
        $columns = $line.Split(';', 5)
        if ($columns.Count -lt 5 -or $columns[0] -notmatch '^U\+([0-9A-Fa-f]{4,6})$' -or -not $columns[4]) { continue }
        $codePoint = [Convert]::ToInt32($Matches[1], 16)
        if ($codePoint -eq 0xFE0F) { continue }
        if (-not $synonyms.ContainsKey($codePoint)) { $synonyms[$codePoint] = [System.Collections.Generic.List[string]]::new() }
        foreach ($word in ($columns[4] -split ',')) {
            if ($word.Trim()) { $synonyms[$codePoint].Add($word.Trim()) }
        }
    }
}

# emoji-test.txt lesen
$version = ''
$groups = [System.Collections.Generic.List[string]]::new()
$entries = [System.Collections.Generic.List[object]]::new()
$group = -1
$subgroup = ''
$tones = 0x1F3FB..0x1F3FF
foreach ($line in Get-Content -Encoding UTF8 $testPath) {
    if ($line -match '^# Version: (.+)$') { $version = $Matches[1]; continue }
    if ($line -match '^# group: (.+)$') {
        $name = $Matches[1].Trim()
        if ($name -eq 'Component') { $group = -1 } else { $groups.Add($name); $group = $groups.Count - 1 }
        continue
    }
    if ($line -match '^# subgroup: (.+)$') { $subgroup = $Matches[1].Trim(); continue }
    if ($line -match '^([0-9A-F ]+?)\s*;\s*fully-qualified\s*#\s*(\S+)\s+E(\d+\.\d+)\s+(.+)$' -and $group -ge 0) {
        $sequence = $Matches[2]
        $codePoints = Get-CodePoints $sequence
        if ($codePoints | Where-Object { ($_ -ge 0x1F1E6 -and $_ -le 0x1F1FF) -or ($_ -ge 0xE0020 -and $_ -le 0xE007F) }) { continue }
        $entries.Add([pscustomobject]@{ Sequence = $sequence; Group = $group; Subgroup = $subgroup; Version = $Matches[3]; EnglishName = $Matches[4]; CodePoints = $codePoints })
    }
}

# Hauttöne den Basis-Emojis zuordnen
$byKey = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$bases = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $entries) {
    $used = @($entry.CodePoints | Where-Object { $_ -in $tones } | Select-Object -Unique)
    if ($used.Count -eq 0) {
        $entry | Add-Member -NotePropertyName Variants -NotePropertyValue ([object[]]::new(5)) -Force
        $byKey[(Remove-Fe0f $entry.Sequence)] = $entry
        $bases.Add($entry)
    }
}
foreach ($entry in $entries) {
    $used = @($entry.CodePoints | Where-Object { $_ -in $tones } | Select-Object -Unique)
    if ($used.Count -ne 1) { continue }
    $baseText = Remove-Fe0f (-join ($entry.CodePoints | Where-Object { $_ -notin $tones } | ForEach-Object { [char]::ConvertFromUtf32($_) }))
    if ($byKey.ContainsKey($baseText)) {
        $byKey[$baseText].Variants[$used[0] - 0x1F3FB] = $entry.Sequence
    }
}

$emojis = foreach ($entry in $bases) {
    $item = [ordered]@{ s = $entry.Sequence; g = $entry.Group; sg = $entry.Subgroup; v = $entry.Version }
    $names = [ordered]@{}
    $keywords = [ordered]@{}
    foreach ($lang in 'de', 'en') {
        $annotation = Find-Annotation $lang $entry.Sequence
        $name = if ($annotation -and $annotation.tts) { [string]$annotation.tts[0] } elseif ($lang -eq 'en') { $entry.EnglishName } else { $null }
        $words = if ($annotation -and $annotation.default) { @($annotation.default | Where-Object { $_ -ne $name }) } else { @() }
        if ($name) { $names[$lang] = $name }
        if ($words.Count -gt 0) { $keywords[$lang] = [string[]]$words }
    }
    if (-not $names.Contains('de')) { $names['de'] = $names['en'] }
    $item.n = $names
    if ($keywords.Count -gt 0) { $item.k = $keywords }
    $baseCp = ($entry.CodePoints | Where-Object { $_ -ne 0xFE0F } | Select-Object -First 1)
    if ($entry.CodePoints.Count -le 2 -or ($entry.CodePoints | Where-Object { $_ -ne 0xFE0F }).Count -eq 1) {
        if ($synonyms.ContainsKey($baseCp)) { $item.y = [string[]]@($synonyms[$baseCp] | Select-Object -Unique) }
    }
    if ($entry.Variants | Where-Object { $_ }) { $item.t = $entry.Variants }
    $item
}

$data = [ordered]@{
    source = [ordered]@{
        unicodeEmojiVersion = $version
        emojiTest           = "$UnicodeBase/emoji-test.txt"
        cldr                = "$CldrBase (annotations, annotationsDerived; de, en)"
        symbolsCsv          = [bool]$SymbolsCsv
        license             = 'Unicode License v3 (https://www.unicode.org/license.txt)'
    }
    groups = $groups
    emojis = @($emojis)
}

$jsonBytes = [Text.Encoding]::UTF8.GetBytes(($data | ConvertTo-Json -Depth 6 -Compress))
$target = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
$stream = [IO.File]::Create($target)
try {
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionLevel]::SmallestSize)
    $gzip.Write($jsonBytes, 0, $jsonBytes.Length)
    $gzip.Dispose()
}
finally {
    $stream.Dispose()
}

Write-Host ("{0} Emojis in {1} Gruppen, Unicode {2}, {3:N0} KB -> {4}" -f @($emojis).Count, $groups.Count, $version, ((Get-Item $target).Length / 1KB), $target)
