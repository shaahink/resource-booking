import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApiClient } from './api-client';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let endSession: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ApiClient, useValue: {} },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    endSession = vi
      .spyOn(TestBed.inject(AuthService), 'endSession')
      .mockImplementation(() => undefined);
  });

  const respond = (url: string, status: number) => {
    http.get(url).subscribe({ error: () => undefined });
    backend.expectOne(url).flush(null, { status, statusText: 'x' });
  };

  it('ends the session on a 401 from an API call', () => {
    respond('/api/bookings/mine', 401);
    expect(endSession).toHaveBeenCalledOnce();
  });

  it('leaves the auth endpoints and other errors alone', () => {
    respond('/api/auth/me', 401);
    respond('/api/auth/login', 401);
    respond('/api/bookings/mine', 403);
    expect(endSession).not.toHaveBeenCalled();
  });
});
