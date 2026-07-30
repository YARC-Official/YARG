#!/bin/sh
dotnet build LibVLCSharp/src/LibVLCSharp/LibVLCSharp.csproj -c Release -p:DefineConstants="UNITY DESKTOP" -f netstandard2.1 -o .
