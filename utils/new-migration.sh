#!/bin/bash

clear

dotnet ef migrations add "$1" --startup-project jff-agent --project jff-agent-infra --context JffAgentDbContext