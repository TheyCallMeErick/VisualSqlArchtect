global using System;
global using System.IO;
global using System.Linq;
global using System.Data;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Reflection;
global using System.ComponentModel;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Threading;
global using System.Threading.Tasks;

global using Xunit;

global using Avalonia;
global using Avalonia.Controls;
global using Avalonia.Input;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using AkkornStudio.Core;
global using AkkornStudio.Ddl;
global using AkkornStudio.Metadata;
global using AkkornStudio.Nodes;
global using AkkornStudio.Nodes.Pins;
global using AkkornStudio.Providers;
global using AkkornStudio.QueryEngine;
global using AkkornStudio.Registry;
global using AkkornStudio.Compilation;
global using AkkornStudio.CanvasKit;
global using AkkornStudio.UI;
global using AkkornStudio.UI.Services;
global using AkkornStudio.UI.Services.Localization;
global using AkkornStudio.UI.Services.CommandPalette;
global using AkkornStudio.UI.Services.Connection;
global using AkkornStudio.UI.Services.ConnectionManager;
global using AkkornStudio.UI.Services.ConnectionManager.Models;
global using AkkornStudio.UI.Services.QueryPreview;
global using AkkornStudio.UI.Services.Validation;
global using AkkornStudio.UI.Serialization;
global using AkkornStudio.UI.ViewModels;
global using AkkornStudio.UI.ViewModels.Canvas;
global using AkkornStudio.UI.ViewModels.Canvas.Strategies;
global using AkkornStudio.UI.ViewModels.UndoRedo;
global using AkkornStudio.UI.ViewModels.UndoRedo.Commands;

global using DataType = AkkornStudio.Nodes.PinDataType;
global using EDiagnosticStatus = AkkornStudio.UI.Services.AppDiagnostics.Models.DiagnosticStatus;
global using EGuardSeverity = AkkornStudio.UI.Services.Validation.GuardSeverity;
global using EIssueSeverity = AkkornStudio.UI.Services.Validation.IssueSeverity;
global using EPreviewExecutionState = AkkornStudio.UI.ViewModels.Canvas.PreviewExecutionState;
global using EConnectionActivationOutcome = AkkornStudio.UI.Services.ConnectionManager.ConnectionActivationOutcome;
global using EConnectionHealthStatus = AkkornStudio.UI.ViewModels.ConnectionHealthStatus;
