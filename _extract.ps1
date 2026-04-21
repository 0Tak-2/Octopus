$c = Get-Content 'C:\Users\yt031\.cursor\projects\t-OCTO-Octopus\agent-transcripts\303b84ec-5286-47e3-ad3f-6a41ccf3d030\303b84ec-5286-47e3-ad3f-6a41ccf3d030.jsonl' -TotalCount 400
$j = $c[-1] | ConvertFrom-Json
$j.message.content[0].text | Out-File -FilePath T:\OCTO\Octopus\_tmp_log400.txt -Encoding utf8
