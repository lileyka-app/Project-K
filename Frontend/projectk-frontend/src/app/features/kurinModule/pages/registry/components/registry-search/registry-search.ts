import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { InputIconModule } from '@openng/optimus-ui/inputicon';

/**
 * The registry's search field, the same in every mode that searches people. Its label is there for
 * screen readers only: the magnifier and the placeholder already say what it is.
 */
@Component({
  selector: 'app-registry-search',
  imports: [FormsModule, InputTextModule, IconFieldModule, InputIconModule],
  templateUrl: './registry-search.html',
  styleUrl: './registry-search.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegistrySearchComponent {
  readonly value = model('');
  readonly inputId = input.required<string>();
  readonly label = input.required<string>();
  readonly placeholder = input('Пошук');
}
