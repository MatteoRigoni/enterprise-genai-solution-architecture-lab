# Loading Service - Guida all'Uso

## Panoramica

Il servizio di loading centralizzato (`ILoadingService`) gestisce lo stato di caricamento dell'applicazione in modo unificato, mostrando automaticamente un overlay con `FluentProgressRing` quando sono in corso operazioni asincrone.

## Architettura

### Componenti Principali

1. **ILoadingService** - Interfaccia del servizio
2. **LoadingService** - Implementazione scoped (una per sessione utente)
3. **LoadingOverlay** - Componente globale integrato nel `MainLayout`
4. **FluentProgressRing** - Componente Fluent UI per l'indicatore di caricamento

### Caratteristiche

- ✅ **Gestione multipla**: Supporta più operazioni di loading simultanee con chiavi univoche
- ✅ **Thread-safe**: Utilizza lock per garantire la sicurezza in ambienti concorrenti
- ✅ **Event-driven**: Notifica i cambiamenti di stato tramite eventi
- ✅ **Automatic cleanup**: Pattern IDisposable per la pulizia delle risorse
- ✅ **Cancellation token support**: Integrazione con `CancellationToken` per operazioni asincrone

## Best Practice

### 1. Uso del Servizio

#### Pattern Consigliato: `ExecuteWithLoadingAsync`

Il metodo più semplice e sicuro per gestire il loading:

```csharp
[Inject]
private ILoadingService LoadingService { get; set; } = default!;

private async Task LoadDataAsync()
{
    await LoadingService.ExecuteWithLoadingAsync(async cancellationToken =>
    {
        var data = await Http.GetFromJsonAsync<DataModel>("/api/data", cancellationToken);
        // Process data...
    }, key: "load-data");
}
```

**Vantaggi:**
- Gestione automatica di start/stop
- Gestione errori garantita (finally block)
- Supporto per cancellation token
- Codice più pulito e manutenibile

#### Pattern Manuale: Start/Stop

Per casi più complessi dove serve controllo fine:

```csharp
private async Task ComplexOperationAsync()
{
    LoadingService.StartLoading("complex-operation");
    try
    {
        // Step 1
        await Step1Async();
        
        // Step 2 (può essere condizionale)
        if (condition)
        {
            await Step2Async();
        }
    }
    finally
    {
        LoadingService.StopLoading("complex-operation");
    }
}
```

### 2. Chiavi di Loading

**Usa chiavi specifiche** per operazioni distinte:

```csharp
// ✅ Buono: chiavi specifiche
LoadingService.StartLoading("chat-send");
LoadingService.StartLoading("document-upload");
LoadingService.StartLoading("data-export");

// ❌ Evita: chiave di default per tutto
LoadingService.StartLoading(); // Usa solo se c'è una singola operazione globale
```

**Vantaggi delle chiavi specifiche:**
- Debug più semplice
- Possibilità di gestire loading multipli indipendentemente
- Migliore tracciabilità

### 3. Loading Multipli

Il servizio supporta **loading multipli simultanei**:

```csharp
// Operazione 1
LoadingService.StartLoading("operation-1");

// Operazione 2 (può partire mentre la 1 è ancora attiva)
LoadingService.StartLoading("operation-2");

// L'overlay rimane visibile finché almeno una operazione è attiva
LoadingService.StopLoading("operation-1");
// Overlay ancora visibile (operation-2 attiva)

LoadingService.StopLoading("operation-2");
// Overlay scompare (nessuna operazione attiva)
```

### 4. Verifica dello Stato

```csharp
// Verifica se c'è almeno un loading attivo
if (LoadingService.IsLoading())
{
    // Disabilita UI
}

// Verifica un loading specifico
if (LoadingService.IsLoading("chat-send"))
{
    // Disabilita solo il pulsante di invio
}
```

### 5. Integrazione con Componenti Blazor

#### Esempio: Chat Component

```csharp
public partial class Chat
{
    [Inject]
    private ILoadingService LoadingService { get; set; } = default!;

    private async Task SendMessageAsync()
    {
        await LoadingService.ExecuteWithLoadingAsync(async ct =>
        {
            var response = await Http.PostAsJsonAsync("/api/chat", request, ct);
            // Process response...
        }, key: "chat-send");
    }
}
```

#### Esempio: Document Upload

```csharp
public partial class Documents
{
    [Inject]
    private ILoadingService LoadingService { get; set; } = default!;

    private async Task UploadDocumentAsync(IBrowserFile file)
    {
        await LoadingService.ExecuteWithLoadingAsync(async ct =>
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(file.OpenReadStream()), "file", file.Name);
            
            var response = await Http.PostAsync("/api/documents", content, ct);
            // Handle response...
        }, key: "document-upload");
    }
}
```

## Personalizzazione

### Modificare il Messaggio di Loading

Per personalizzare il messaggio mostrato nell'overlay, modifica `LoadingOverlay.razor`:

```razor
@if (!string.IsNullOrWhiteSpace(loadingMessage))
{
    <p class="loading-message">@loadingMessage</p>
}
```

E aggiungi un metodo nel servizio per impostare il messaggio (opzionale).

### Styling Personalizzato

Modifica `LoadingOverlay.razor.css` per personalizzare:
- Colore di sfondo dell'overlay
- Dimensione del ProgressRing
- Animazioni
- Posizionamento

## Considerazioni di Performance

1. **Servizio Scoped**: Una istanza per sessione utente (Blazor Server)
2. **Eventi**: Gli eventi sono gestiti in modo efficiente, ma evita troppi listener
3. **Lock**: Il lock è minimale e non dovrebbe impattare le performance

## Testing

### Unit Test del Servizio

```csharp
[Fact]
public void StartLoading_SetsIsLoadingToTrue()
{
    var service = new LoadingService();
    service.StartLoading("test");
    Assert.True(service.IsLoading("test"));
}

[Fact]
public void ExecuteWithLoadingAsync_ManagesStateCorrectly()
{
    var service = new LoadingService();
    var task = service.ExecuteWithLoadingAsync(async ct =>
    {
        await Task.Delay(100, ct);
        return "result";
    }, key: "test");
    
    Assert.True(service.IsLoading("test"));
    var result = task.Result;
    Assert.False(service.IsLoading("test"));
}
```

## Troubleshooting

### Overlay non scompare

- Verifica che `StopLoading` venga chiamato (usa `ExecuteWithLoadingAsync` per sicurezza)
- Controlla che la chiave usata in `StartLoading` corrisponda a quella in `StopLoading`
- Verifica che non ci siano eccezioni non gestite che impediscono il finally block

### Overlay non appare

- Verifica che `LoadingOverlay` sia incluso nel `MainLayout`
- Controlla che il servizio sia registrato in `Program.cs`
- Verifica che `StartLoading` venga effettivamente chiamato

### Performance Issues

- Evita di chiamare `StartLoading`/`StopLoading` troppo frequentemente
- Usa chiavi specifiche invece di creare molte istanze con chiave di default
- Considera di debounce operazioni rapide (< 200ms) per evitare flickering

## Riferimenti

- [Fluent UI Blazor ProgressRing Documentation](https://www.fluentui-blazor.net/ProgressRing)
- [Blazor Server Scoped Services](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection#scoped-services)

