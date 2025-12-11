import { Component, ElementRef, ViewChild, AfterViewChecked, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex flex-col h-screen bg-gray-900 text-white font-sans">
      <!-- Header -->
      <header class="p-4 bg-gray-800 border-b border-gray-700 flex justify-between items-center shadow-md">
        <div class="flex items-center gap-2">
          <span class="text-2xl">🤖</span>
          <h1 class="text-xl font-bold bg-gradient-to-r from-blue-400 to-purple-500 bg-clip-text text-transparent">Codebase RAG</h1>
        </div>
        <button (click)="toggleSettings()" class="p-2 rounded-full hover:bg-gray-700 transition-colors text-gray-400 hover:text-white" title="Settings">
          ⚙️
        </button>
      </header>

      <!-- Settings Modal -->
      <div *ngIf="showSettings" class="absolute inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 transition-opacity">
        <div class="bg-gray-800 p-6 rounded-xl shadow-2xl w-[600px] border border-gray-700 transform transition-all scale-100 flex flex-col max-h-[90vh]">
          <h2 class="text-xl font-bold mb-4 text-white">Indexing Settings</h2>
          
          <div class="mb-4 flex-1 overflow-hidden flex flex-col">
            <label class="block text-sm font-medium mb-2 text-gray-300">Codebase Root Path</label>
            <div class="flex gap-2 mb-2">
                <input [(ngModel)]="rootPath" readonly class="flex-1 p-2 bg-gray-900 rounded-lg border border-gray-600 outline-none text-gray-400 cursor-not-allowed">
                 <button (click)="browse(rootPath)" *ngIf="rootPath" class="p-2 bg-gray-700 hover:bg-gray-600 rounded-lg" title="Reload current directory">↻</button>
                 <button (click)="navigateUp()" [disabled]="!canNavigateUp" class="p-2 bg-gray-700 hover:bg-gray-600 rounded-lg disabled:opacity-50" title="Go Up">⬆️</button>
            </div>
            
            <div class="flex-1 overflow-y-auto bg-gray-900 border border-gray-600 rounded-lg p-2 min-h-[300px]">
                <div *ngIf="isLoadingDirs" class="flex justify-center p-4">
                    <span class="animate-spin text-blue-500">↻</span>
                </div>
                <div *ngIf="!isLoadingDirs">
                    <div *ngFor="let item of directoryItems" 
                        (click)="item.type === 'Drive' || item.type === 'Folder' ? browse(item.path) : null"
                        class="p-2 hover:bg-gray-800 cursor-pointer flex items-center gap-2 text-sm border-b border-gray-800 last:border-0">
                        <span>{{ item.type === 'Drive' ? '💾' : 'Ep' }}</span>
                        <span class="truncate">{{ item.name }}</span>
                    </div>
                     <div *ngIf="directoryItems.length === 0" class="text-gray-500 text-center p-4">No folders found</div>
                </div>
                <!-- Error handling could go here -->
            </div>
            <p class="text-xs text-gray-500 mt-2">Select the folder you want to index.</p>
          </div>

          <div class="flex justify-end gap-3 pt-4 border-t border-gray-700">
            <button (click)="toggleSettings()" class="px-4 py-2 text-gray-300 hover:text-white hover:bg-gray-700 rounded-lg transition-colors">
                {{ isIndexing ? 'Close (Keep Indexing)' : 'Close' }}
            </button>
             
            <button *ngIf="isIndexing" (click)="cancelIndexing()" class="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-500 flex items-center gap-2 transition-colors shadow-lg">
                <span>🛑</span> Cancel
            </button>

            <button *ngIf="!isIndexing" (click)="startIndexing()" [disabled]="!rootPath" class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-500 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 transition-colors shadow-lg shadow-blue-900/20">
              <span>▶️</span> Start Indexing
            </button>
          </div>
           
           <div *ngIf="indexingMessage" class="mt-3 text-sm text-center p-2 bg-gray-900 rounded border border-gray-700">
                <span [class.text-green-400]="!isIndexing && indexingMessage.includes('Completed')" 
                      [class.text-red-400]="!isIndexing && indexingMessage.includes('Failed')"
                      [class.text-blue-400]="isIndexing">
                    {{ indexingMessage }}
                </span>
           </div>
        </div>
      </div>

      <!-- Chat Area -->
      <div class="flex-1 overflow-y-auto p-4 space-y-6 scroll-smooth" #scrollContainer>
        <div *ngIf="messages.length === 0" class="h-full flex flex-col items-center justify-center text-gray-500 opacity-50">
          <div class="text-6xl mb-4">👋</div>
          <p class="text-xl">Ready to explore your code.</p>
        </div>

        <div *ngFor="let msg of messages" [ngClass]="{'items-end': msg.role === 'user', 'items-start': msg.role === 'assistant'}" class="flex flex-col animate-fade-in">
          <div [ngClass]="{'bg-blue-600 text-white': msg.role === 'user', 'bg-gray-800 text-gray-100 border border-gray-700': msg.role === 'assistant'}" class="max-w-[85%] p-4 rounded-2xl shadow-sm whitespace-pre-wrap leading-relaxed">
            <div *ngIf="msg.role === 'assistant'" class="flex items-center gap-2 mb-2 text-xs text-gray-400 font-bold uppercase tracking-wider">
              <span>🤖 Assistant</span>
            </div>
            {{ msg.content }}
          </div>
        </div>
        
        <div *ngIf="isLoading" class="flex items-start animate-pulse">
            <div class="bg-gray-800 p-4 rounded-2xl border border-gray-700 flex gap-2 items-center text-gray-400">
              <span class="animate-bounce">●</span>
              <span class="animate-bounce delay-100">●</span>
              <span class="animate-bounce delay-200">●</span>
            </div>
        </div>
      </div>

      <!-- Input Area -->
      <div class="p-4 bg-gray-800 border-t border-gray-700 shadow-lg">
        <div class="max-w-4xl mx-auto flex gap-3">
          <input [(ngModel)]="newMessage" (keyup.enter)="sendMessage()" 
            class="flex-1 p-4 bg-gray-900 rounded-xl border border-gray-600 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 outline-none transition-all placeholder-gray-500" 
            placeholder="Ask a question about your code..." [disabled]="isLoading">
          <button (click)="sendMessage()" [disabled]="isLoading || !newMessage.trim()" 
            class="px-6 py-3 bg-blue-600 text-white rounded-xl hover:bg-blue-500 disabled:opacity-50 disabled:cursor-not-allowed font-bold transition-all shadow-lg shadow-blue-900/20 flex items-center gap-2">
            <span>Send</span>
            <span class="text-xl">➤</span>
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .animate-fade-in { animation: fadeIn 0.3s ease-out; }
    @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
    /* Custom Scrollbar */
    ::-webkit-scrollbar { width: 8px; }
    ::-webkit-scrollbar-track { background: #111827; }
    ::-webkit-scrollbar-thumb { background: #374151; border-radius: 4px; }
    ::-webkit-scrollbar-thumb:hover { background: #4b5563; }
  `]
})
export class ChatComponent implements AfterViewChecked, OnInit {
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;

  messages: { role: 'user' | 'assistant', content: string }[] = [];
  newMessage = '';
  isLoading = false;
  showSettings = false;

  // Settings
  rootPath = '';
  isIndexing = false;
  indexingMessage = '';

  // Browser
  directoryItems: any[] = [];
  isLoadingDirs = false;
  canNavigateUp = false;

  constructor(private apiService: ApiService) { }

  ngOnInit() {
    // Check initial status
    this.pollStatus();
  }

  ngAfterViewChecked() {
    this.scrollToBottom();
  }

  scrollToBottom(): void {
    try {
      this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
    } catch (err) { }
  }

  sendMessage() {
    if (!this.newMessage.trim()) return;

    const userMsg = this.newMessage;
    this.messages.push({ role: 'user', content: userMsg });
    this.newMessage = '';
    this.isLoading = true;

    this.apiService.chat(userMsg).subscribe({
      next: (res) => {
        this.messages.push({ role: 'assistant', content: res.answer });
        this.isLoading = false;
      },
      error: (err) => {
        this.messages.push({ role: 'assistant', content: 'Error: Could not get response. Ensure the API is running.' });
        this.isLoading = false;
      }
    });
  }

  toggleSettings() {
    this.showSettings = !this.showSettings;
    if (this.showSettings && !this.directoryItems.length) {
      this.browse(''); // Load drives initially
    }
  }

  browse(path: string) {
    this.isLoadingDirs = true;
    this.apiService.browse(path).subscribe({
      next: (items) => {
        this.directoryItems = items;
        this.rootPath = path;
        this.isLoadingDirs = false;
        // Simple check for up navigation
        this.canNavigateUp = !!path && path.length > 3;
      },
      error: (err) => {
        console.error(err);
        this.isLoadingDirs = false;
        alert('Failed to load directory: ' + err.message);
      }
    });
  }

  navigateUp() {
    if (!this.rootPath) return;
    // Simple parent detection, could be improved
    const parts = this.rootPath.split(/[/\\]/);
    parts.pop(); // remove current
    // If we are at drive root (e.g. C: or C:\), parts might be weird, handling simply:
    if (parts.length <= 1 && this.rootPath.length <= 3) {
      this.browse('');
      return;
    }
    const parent = parts.join('\\') || parts.join('/');
    this.browse(parent || ''); // if empty back to drives
  }

  startIndexing() {
    if (!this.rootPath) return;
    this.isIndexing = true;
    this.indexingMessage = 'Starting...';

    this.apiService.rebuildIndex(this.rootPath).subscribe({
      next: () => {
        this.pollStatus();
      },
      error: (err) => {
        console.error('Indexing error:', err);
        this.isIndexing = false;
        this.indexingMessage = `Error starting: ${err.message}`;
      }
    });
  }

  cancelIndexing() {
    this.apiService.cancelIndexing().subscribe({
      next: () => {
        this.indexingMessage = "Cancellation requested...";
      }
    });
  }

  pollStatus() {
    const intervalId = setInterval(() => {
      this.apiService.getIndexingStatus().subscribe({
        next: (status) => {
          const wasIndexing = this.isIndexing;
          this.isIndexing = status.isIndexing;
          if (status.message) this.indexingMessage = status.message;

          if (!status.isIndexing) {
            clearInterval(intervalId);

            // Show alert if we were indexing and it just finished
            if (wasIndexing && status.message) {
              alert(status.message);
            }
          }
        },
        error: () => {
          clearInterval(intervalId);
          this.isIndexing = false;
        }
      });
    }, 2000);
  }
}
