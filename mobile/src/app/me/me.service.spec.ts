import { dutiesOf } from './me.service';
import { MyDutiesDto } from './me.models';

describe('dutiesOf', () => {
  const kurin = { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true };

  it('passes the server’s «Справи» through as they are', () => {
    const reply: MyDutiesDto = {
      items: [{ kind: 'BadgesToReview', kurin, count: 2, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null, key: 'BadgesToReview:k1:-:-:-', fingerprint: '2' }],
      hiddenCount: 3,
    };
    expect(dutiesOf(reply)).toBe(reply);
  });

  it('reads the bare list of an older server as nothing hidden and nothing to hide', () => {
    const read = dutiesOf([{ kind: 'BadgesToReview', kurin, count: 2, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null }]);
    expect(read.hiddenCount).toBe(0);
    expect(read.items[0].key).toBe('');
  });
});
