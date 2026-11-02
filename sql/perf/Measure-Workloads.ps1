<#
    Measures the workloads of PERFORMANCE.md on a performance database and keeps the evidence.

    For each workload: one warm-up run, then -Runs runs with SET STATISTICS IO, TIME ON (the median of CPU and elapsed is
    reported, so one slow run does not decide), then one run with SET STATISTICS XML ON for the actual plan. The plan run
    is separate because producing a plan costs CPU itself. Queries go through parameters, like the application's.

        powershell -File sql/perf/Measure-Workloads.ps1 -Phase 0-baseline
        powershell -File sql/perf/Measure-Workloads.ps1 -Phase 1-payments -Only W1a,W3

    Writes docs/perf/raw/<Phase>/<Id>.txt, docs/perf/raw/<Phase>/summary.csv and docs/perf/plans/<Phase>/<Id>.sqlplan.
    With -Only, the summary of the phase is extended, not replaced.
#>
param(
    [Parameter(Mandatory)][string]$Phase,
    [string]$Server = 'localhost',
    [string]$Database = 'ApexPostDb_Perf',
    [int]$Runs = 5,
    [string[]]$Only = @()
)

$ErrorActionPreference = 'Stop'
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$rawFolder = Join-Path $root "docs\perf\raw\$Phase"
$planFolder = Join-Path $root "docs\perf\plans\$Phase"
New-Item -ItemType Directory -Force -Path $rawFolder, $planFolder | Out-Null

$from = '2026-10-18'
$to = '2026-11-01'
$year = '2025-11-02'
$dates = @(@{ Name = '@From'; Type = 'Date'; Value = $from }, @{ Name = '@To'; Type = 'Date'; Value = $to })
$oneBranch = @{ Name = '@BranchCode'; Type = 'VarChar'; Value = 'BR001' }
$allBranches = @{ Name = '@BranchCode'; Type = 'VarChar'; Value = [DBNull]::Value }
$efRange = @(
    @{ Name = '@start'; Type = 'DateTime2'; Value = $from },
    @{ Name = '@end'; Type = 'DateTime2'; Value = '2026-11-02' }
)
$efBranch = @{ Name = '@branch'; Type = 'VarChar'; Value = 'BR001' }
$efPage = @(@{ Name = '@p'; Type = 'Int'; Value = 0 }, @{ Name = '@p2'; Type = 'Int'; Value = 20 })

$procedureSales = 'EXEC dbo.usp_DailyBranchSalesRange @From = @From, @To = @To, @BranchCode = @BranchCode'
$procedureStock = 'EXEC dbo.usp_LowStockReport @Threshold = @Threshold, @BranchCode = @BranchCode'
$threshold = @{ Name = '@Threshold'; Type = 'Int'; Value = 5 }

$workloads = @(
    @{ Id = 'W1a'; Title = 'usp_DailyBranchSalesRange, all branches, 15 days (Auditor report)'; Text = $procedureSales
       Parameters = $dates + $allBranches }
    @{ Id = 'W1b'; Title = 'usp_DailyBranchSalesRange, one branch, 15 days (Manager report)'; Text = $procedureSales
       Parameters = $dates + $oneBranch }
    @{ Id = 'W1c'; Title = 'usp_DailyBranchSalesRange, one branch, one day (dashboard)'; Text = $procedureSales
       Parameters = @(@{ Name = '@From'; Type = 'Date'; Value = $to }, @{ Name = '@To'; Type = 'Date'; Value = $to }, $oneBranch) }
    @{ Id = 'W2a'; Title = 'GET /Transactions: page 1 of a branch (EF Core)'; File = 'sql\perf\workloads\transaction-page.sql'
       Parameters = @($efBranch) + $efPage }
    @{ Id = 'W2b'; Title = 'GET /Transactions: total of a branch (EF Core)'; File = 'sql\perf\workloads\transaction-count.sql'
       Parameters = @($efBranch) }
    @{ Id = 'W2c'; Title = 'GET /Transactions: page 1, all branches (EF Core)'; File = 'sql\perf\workloads\transaction-page-all.sql'
       Parameters = $efPage }
    @{ Id = 'W3'; Title = 'GET /Reports/ShippingVolume, one branch (EF Core)'; File = 'sql\perf\workloads\shipping-groups.sql'
       Parameters = $efRange + $efBranch }
    @{ Id = 'W4-case'; Title = 'Money order total, CASE predicate (EF Core, original)'; File = 'sql\perf\workloads\money-orders-case.sql'
       Parameters = $efRange + $efBranch }
    @{ Id = 'W4-sarg'; Title = 'Money order total, two ranges (EF Core, fixed)'; File = 'sql\perf\workloads\money-orders-sarg.sql'
       Parameters = $efRange + $efBranch + @(
           @{ Name = '@start0'; Type = 'DateTime2'; Value = $from },
           @{ Name = '@end0'; Type = 'DateTime2'; Value = '2026-11-02' }) }
    @{ Id = 'W4a-case'; Title = 'Money order total, all branches, CASE predicate'; File = 'sql\perf\workloads\money-orders-case-all.sql'
       Parameters = $efRange }
    @{ Id = 'W4a-sarg'; Title = 'Money order total, all branches, two ranges'; File = 'sql\perf\workloads\money-orders-sarg-all.sql'
       Parameters = $efRange + @(
           @{ Name = '@start0'; Type = 'DateTime2'; Value = $from },
           @{ Name = '@end0'; Type = 'DateTime2'; Value = '2026-11-02' }) }
    @{ Id = 'W5'; Title = 'Top 10 products per region, 30 days'; File = 'sql\reports\03-top-products-by-region.sql'
       Parameters = @(@{ Name = '@From'; Type = 'Date'; Value = '2026-10-02' }, @{ Name = '@To'; Type = 'Date'; Value = $to },
                      @{ Name = '@Top'; Type = 'Int'; Value = 10 }) }
    @{ Id = 'W5y'; Title = 'Top 10 products per region, one year'; File = 'sql\reports\03-top-products-by-region.sql'
       Parameters = @(@{ Name = '@From'; Type = 'Date'; Value = $year }, @{ Name = '@To'; Type = 'Date'; Value = $to },
                      @{ Name = '@Top'; Type = 'Int'; Value = 10 }) }
    @{ Id = 'W6a'; Title = 'usp_LowStockReport, all branches'; Text = $procedureStock; Parameters = @($threshold, $allBranches) }
    @{ Id = 'W6b'; Title = 'usp_LowStockReport, one branch'; Text = $procedureStock; Parameters = @($threshold, $oneBranch) }
    @{ Id = 'W7'; Title = 'Branches with no sales on a day'; File = 'sql\reports\02-branches-without-sales.sql'
       Parameters = @(@{ Name = '@Day'; Type = 'Date'; Value = $to }) }
    @{ Id = 'R1'; Title = 'Report 1: daily branch sales'; File = 'sql\reports\01-daily-branch-sales.sql'
       Parameters = $dates + $allBranches + @{ Name = '@MinTransactions'; Type = 'Int'; Value = 1 } }
    @{ Id = 'R1-apply'; Title = 'Report 1, first version with OUTER APPLY'; File = 'sql\perf\workloads\daily-sales-outer-apply.sql'
       Parameters = $dates + $allBranches + @{ Name = '@MinTransactions'; Type = 'Int'; Value = 1 } }
    @{ Id = 'R4'; Title = 'Report 4: shipping volume'; File = 'sql\reports\04-shipping-volume.sql'
       Parameters = $dates + $allBranches + @{ Name = '@MinShipments'; Type = 'Int'; Value = 1 } }
    @{ Id = 'R5'; Title = 'Report 5: money orders and verifications'; File = 'sql\reports\05-money-orders-and-verifications.sql'
       Parameters = $dates + @{ Name = '@MinActivity'; Type = 'Int'; Value = 0 } }
    @{ Id = 'WR'; Title = 'Write: 1000 sales inserted and rolled back'; File = 'sql\perf\workloads\write-1000-sales.sql'
       Parameters = @(); SumStatements = $true }
)

function Get-Text($workload) {
    if ($workload.Text) { return $workload.Text }
    $text = Get-Content -Raw -Path (Join-Path $root $workload.File)
    $marker = '-- query'
    $at = $text.IndexOf($marker)
    if ($at -lt 0) { return $text }
    return $text.Substring($at + $marker.Length)
}

function New-Command($connection, $text, $parameters) {
    $command = $connection.CreateCommand()
    $command.CommandText = $text
    $command.CommandTimeout = 600
    foreach ($parameter in $parameters) {
        $type = [System.Data.SqlDbType]::($parameter.Type)
        $added = $command.Parameters.Add($parameter.Name, $type)
        if ($parameter.Type -eq 'VarChar') { $added.Size = 5 }
        $added.Value = $parameter.Value
    }
    return $command
}

function Invoke-Once($connection, $text, $parameters) {
    $command = New-Command $connection $text $parameters
    $reader = $command.ExecuteReader()
    $rows = 0
    $plans = New-Object System.Collections.ArrayList
    $value = $null
    do {
        $isPlan = $reader.FieldCount -eq 1 -and $reader.GetName(0) -like '*XML Showplan*'
        while ($reader.Read()) {
            if ($isPlan) { [void]$plans.Add($reader.GetString(0)) } else { $rows++; $value = $reader.GetValue(0) }
        }
    } while ($reader.NextResult())
    $reader.Close()
    return @{ Rows = $rows; Plans = $plans; Value = $value }
}

function Read-Statistics([string]$messages, [bool]$sumStatements = $false) {
    $tables = [ordered]@{}
    foreach ($match in [regex]::Matches($messages, "Table '([^']+)'\. Scan count (\d+), logical reads (\d+), physical reads (\d+)")) {
        $name = $match.Groups[1].Value
        if (-not $tables.Contains($name)) { $tables[$name] = 0 }
        $tables[$name] += [long]$match.Groups[3].Value
    }
    $execution = [regex]::Matches($messages, 'SQL Server Execution Times:\s+CPU time = (\d+) ms,\s+elapsed time = (\d+) ms')
    $compile = [regex]::Matches($messages, 'SQL Server parse and compile time:\s+CPU time = (\d+) ms,\s+elapsed time = (\d+) ms')
    $compileCpu = 0
    foreach ($match in $compile) { $compileCpu += [int]$match.Groups[1].Value }
    $last = $execution[$execution.Count - 1]
    $cpu = [int]$last.Groups[1].Value
    $elapsed = [int]$last.Groups[2].Value
    if ($sumStatements) {
        $cpu = 0
        $elapsed = 0
        foreach ($match in $execution) {
            $cpu += [int]$match.Groups[1].Value
            $elapsed += [int]$match.Groups[2].Value
        }
    }
    return @{
        Tables = $tables
        Reads = ($tables.Values | Measure-Object -Sum).Sum
        Cpu = $cpu
        Elapsed = $elapsed
        CompileCpu = $compileCpu
    }
}

function Get-Median([int[]]$values) {
    $sorted = @($values | Sort-Object)
    return $sorted[[int][math]::Floor($sorted.Count / 2)]
}

function Read-Plan([string]$xml) {
    $namespace = @{ s = 'http://schemas.microsoft.com/sqlserver/2004/07/showplan' }
    $document = [xml]$xml
    $lines = New-Object System.Collections.ArrayList
    $accesses = New-Object System.Collections.ArrayList
    foreach ($node in (Select-Xml -Xml $document -Namespace $namespace -XPath '//s:RelOp').Node) {
        $actual = 0L
        $counters = @($node.ChildNodes | Where-Object { $_.LocalName -eq 'RunTimeInformation' })
        if ($counters.Count -gt 0) {
            foreach ($thread in $counters[0].ChildNodes) { $actual += [long]$thread.ActualRows }
        }
        $estimateExecutions = 1 + [double]$node.EstimateRebinds + [double]$node.EstimateRewinds
        $estimate = [math]::Round([double]$node.EstimateRows * $estimateExecutions, 1)
        $object = $null
        foreach ($child in $node.ChildNodes) {
            if ($child.LocalName -in 'IndexScan', 'TableScan') {
                $target = @($child.ChildNodes | Where-Object { $_.LocalName -eq 'Object' })[0]
                if ($target) {
                    $object = ('{0}.{1}' -f $target.Table, $target.Index).Replace('[', '').Replace(']', '')
                }
            }
        }
        $operator = $node.PhysicalOp
        [void]$lines.Add(('{0,-24} {1,-58} est {2,12} act {3,10}' -f $operator, $object, $estimate, $actual))
        if ($object) { [void]$accesses.Add("$operator $object") }
    }
    $query = (Select-Xml -Xml $document -Namespace $namespace -XPath '//s:QueryPlan').Node | Select-Object -First 1
    $warnings = @((Select-Xml -Xml $document -Namespace $namespace -XPath '//s:Warnings/*').Node | ForEach-Object { $_.LocalName } | Select-Object -Unique)
    $missing = @((Select-Xml -Xml $document -Namespace $namespace -XPath '//s:MissingIndexGroup').Node).Count
    return @{
        Lines = $lines
        Accesses = (($accesses | Sort-Object -Unique) -join '; ')
        Dop = $query.DegreeOfParallelism
        GrantedKb = if ($query.MemoryGrantInfo) { $query.MemoryGrantInfo.GrantedMemory } else { 0 }
        Warnings = ($warnings -join ',')
        MissingIndexes = $missing
    }
}

$connection = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;Pooling=False"
$script:log = New-Object System.Text.StringBuilder
$connection.add_InfoMessage({ param($sender, $event) [void]$script:log.AppendLine($event.Message) })
$connection.Open()

function Set-Option([string]$text) {
    $command = $connection.CreateCommand()
    $command.CommandText = $text
    [void]$command.ExecuteNonQuery()
}

Set-Option 'ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;'
$results = New-Object System.Collections.ArrayList

foreach ($workload in $workloads) {
    if ($Only.Count -gt 0 -and $Only -notcontains $workload.Id) { continue }
    $text = Get-Text $workload
    $raw = New-Object System.Text.StringBuilder
    [void]$raw.AppendLine("$($workload.Id): $($workload.Title)")
    [void]$raw.AppendLine("Phase $Phase, $Database, measured $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) UTC")

    Set-Option 'SET STATISTICS IO ON; SET STATISTICS TIME ON;'
    [void]$script:log.Clear()
    $first = Invoke-Once $connection $text $workload.Parameters
    $coldStatistics = Read-Statistics $script:log.ToString() ([bool]$workload.SumStatements)
    [void]$raw.AppendLine(); [void]$raw.AppendLine('--- warm-up run (includes compilation)')
    [void]$raw.Append($script:log.ToString())

    $measured = New-Object System.Collections.ArrayList
    for ($run = 1; $run -le $Runs; $run++) {
        [void]$script:log.Clear()
        $outcome = Invoke-Once $connection $text $workload.Parameters
        $statistics = Read-Statistics $script:log.ToString() ([bool]$workload.SumStatements)
        $statistics.Value = $outcome.Value
        [void]$measured.Add($statistics)
        [void]$raw.AppendLine(); [void]$raw.AppendLine("--- run $run, $($outcome.Rows) rows")
        [void]$raw.Append($script:log.ToString())
    }
    Set-Option 'SET STATISTICS IO OFF; SET STATISTICS TIME OFF;'

    Set-Option 'SET STATISTICS XML ON;'
    $planned = Invoke-Once $connection $text $workload.Parameters
    Set-Option 'SET STATISTICS XML OFF;'
    $plan = $planned.Plans | Sort-Object { ([regex]::Matches($_, '<RelOp ')).Count } -Descending | Select-Object -First 1
    $shape = Read-Plan $plan
    [IO.File]::WriteAllText((Join-Path $planFolder "$($workload.Id).sqlplan"), $plan)
    [void]$raw.AppendLine(); [void]$raw.AppendLine('--- actual plan, largest statement: operator, object, estimated rows, actual rows')
    foreach ($line in $shape.Lines) { [void]$raw.AppendLine($line) }
    [void]$raw.AppendLine("DOP $($shape.Dop), granted KB $($shape.GrantedKb), warnings [$($shape.Warnings)], missing index hints $($shape.MissingIndexes)")
    [IO.File]::WriteAllText((Join-Path $rawFolder "$($workload.Id).txt"), $raw.ToString())

    $tableReads = ($measured[0].Tables.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '
    $summary = [pscustomobject]@{
        Id = $workload.Id
        Title = $workload.Title
        Rows = $planned.Rows
        LogicalReads = $measured[0].Reads
        CpuMs = Get-Median ($measured | ForEach-Object { $_.Cpu })
        ElapsedMs = Get-Median ($measured | ForEach-Object { $_.Elapsed })
        CompileCpuMs = $coldStatistics.CompileCpu
        FirstValue = if ($workload.SumStatements) { [int]$first.Value } else { $null }
        LastValue = if ($workload.SumStatements) { Get-Median ($measured | ForEach-Object { [int]$_.Value }) } else { $null }
        FirstElapsedMs = $coldStatistics.Elapsed
        Dop = $shape.Dop
        GrantedKb = $shape.GrantedKb
        Warnings = $shape.Warnings
        MissingIndexHints = $shape.MissingIndexes
        TableReads = $tableReads
        Access = $shape.Accesses
    }
    [void]$results.Add($summary)
    '{0,-8} rows {1,6} reads {2,8} cpu {3,6} ms elapsed {4,6} ms' -f $summary.Id, $summary.Rows, $summary.LogicalReads, $summary.CpuMs, $summary.ElapsedMs
}
$connection.Close()

$summaryPath = Join-Path $rawFolder 'summary.csv'
if ($Only.Count -gt 0 -and (Test-Path $summaryPath)) {
    $kept = @(Import-Csv $summaryPath | Where-Object { $Only -notcontains $_.Id })
    $results = @($kept) + @($results)
}
$results | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $summaryPath
