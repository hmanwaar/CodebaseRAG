import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class ApiService {
    private apiUrl = 'http://localhost:5020/api';

    constructor(private http: HttpClient) { }

    chat(message: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/Chat`, { message });
    }

    rebuildIndex(rootPath: string, excludePatterns: string[] = []): Observable<any> {
        return this.http.post(`${this.apiUrl}/Indexing/rebuild`, { rootPath, excludePatterns });
    }

    cancelIndexing(): Observable<any> {
        return this.http.post(`${this.apiUrl}/Indexing/cancel`, {});
    }

    getIndexingStatus(): Observable<any> {
        return this.http.get(`${this.apiUrl}/Indexing/status`);
    }

    getIndexedFiles(rootPath: string): Observable<any> {
        return this.http.get(`${this.apiUrl}/Indexing/files`, { params: { rootPath } });
    }

    browse(path: string = ''): Observable<any> {
        return this.http.get(`${this.apiUrl}/Indexing/browse`, { params: { path } });
    }
}
