import { trimTrailingSlashes } from './trim-trailing-slashes.function';

describe('trimTrailingSlashes', () => {
  it('drops every trailing slash', () => {
    expect(trimTrailingSlashes('https://docs.example.org///')).toBe('https://docs.example.org');
  });

  it('keeps a value without one as it is', () => {
    expect(trimTrailingSlashes('https://docs.example.org/user')).toBe('https://docs.example.org/user');
  });

  it('leaves nothing of a value made only of slashes', () => {
    expect(trimTrailingSlashes('//')).toBe('');
  });
});
