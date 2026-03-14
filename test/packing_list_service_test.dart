import 'package:flutter_test/flutter_test.dart';
import 'package:everything/models/packing_entry.dart';
import 'package:everything/core/services/packing_list_service.dart';

void main() {
  const service = PackingListService();
  final now = DateTime.now();

  PackingItem item(String id, String name, PackingCategory cat,
      {bool packed = false, bool essential = false, int qty = 1}) =>
    PackingItem(id: id, name: name, category: cat, quantity: qty, isPacked: packed, isEssential: essential);

  PackingList makeList(String id, String name, List<PackingItem> items, {
    TripType type = TripType.custom, DateTime? departure, DateTime? returnDate, String? destination,
  }) => PackingList(id: id, name: name, tripType: type, items: items,
    departureDate: departure, returnDate: returnDate, destination: destination, createdAt: now);

  group('PackingCategory', () {
    test('all values have label and emoji', () {
      for (final c in PackingCategory.values) { expect(c.label, isNotEmpty); expect(c.emoji, isNotEmpty); }
    });
  });

  group('TripType', () {
    test('all values have label', () {
      for (final t in TripType.values) expect(t.label, isNotEmpty);
    });
  });

  group('PackingItem', () {
    test('defaults', () {
      final i = item('1', 'Shirt', PackingCategory.clothing);
      expect(i.quantity, 1); expect(i.isPacked, false); expect(i.isEssential, false); expect(i.notes, isNull);
    });

    test('copyWith', () {
      final packed = item('1', 'Shirt', PackingCategory.clothing).copyWith(isPacked: true);
      expect(packed.isPacked, true); expect(packed.name, 'Shirt'); expect(packed.id, '1');
    });

    test('toJson/fromJson roundtrip', () {
      final i = PackingItem(id: 'x1', name: 'Laptop', category: PackingCategory.electronics,
        quantity: 1, isPacked: true, isEssential: true, notes: 'Work laptop');
      final r = PackingItem.fromJson(i.toJson());
      expect(r.id, 'x1'); expect(r.name, 'Laptop'); expect(r.category, PackingCategory.electronics);
      expect(r.isPacked, true); expect(r.isEssential, true); expect(r.notes, 'Work laptop');
    });
  });

  group('PackingList', () {
    test('counts packed/unpacked items', () {
      final l = makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing, packed: true),
        item('2', 'B', PackingCategory.clothing),
        item('3', 'C', PackingCategory.clothing, packed: true),
      ]);
      expect(l.totalItems, 3); expect(l.packedItems, 2); expect(l.unpackedItems, 1);
    });

    test('tracks essential items', () {
      final l = makeList('l1', 'Trip', [
        item('1', 'Passport', PackingCategory.documents, essential: true, packed: true),
        item('2', 'Charger', PackingCategory.electronics, essential: true),
        item('3', 'Hat', PackingCategory.accessories),
      ]);
      expect(l.essentialItems, 2); expect(l.packedEssentials, 1); expect(l.unpackedEssentials, 1);
    });

    test('progress percent', () {
      expect(makeList('l1', 'T', [item('1', 'A', PackingCategory.clothing, packed: true), item('2', 'B', PackingCategory.clothing)]).progressPercent, 50.0);
      expect(makeList('l1', 'T', []).progressPercent, 0);
    });

    test('isComplete', () {
      expect(makeList('l1', 'T', [item('1', 'A', PackingCategory.clothing, packed: true), item('2', 'B', PackingCategory.clothing, packed: true)]).isComplete, true);
      expect(makeList('l1', 'T', [item('1', 'A', PackingCategory.clothing, packed: true), item('2', 'B', PackingCategory.clothing)]).isComplete, false);
      expect(makeList('l1', 'T', []).isComplete, false);
    });

    test('daysUntilDeparture', () {
      expect(makeList('l1', 'T', [], departure: now.add(const Duration(days: 5))).daysUntilDeparture, greaterThanOrEqualTo(4));
      expect(makeList('l1', 'T', []).daysUntilDeparture, isNull);
    });

    test('tripDurationDays', () {
      expect(makeList('l1', 'T', [], departure: DateTime(2026, 3, 1), returnDate: DateTime(2026, 3, 8)).tripDurationDays, 7);
      expect(makeList('l1', 'T', []).tripDurationDays, isNull);
    });

    test('itemsByCategory groups correctly', () {
      final bc = makeList('l1', 'T', [
        item('1', 'Shirt', PackingCategory.clothing), item('2', 'Pants', PackingCategory.clothing),
        item('3', 'Charger', PackingCategory.electronics),
      ]).itemsByCategory;
      expect(bc[PackingCategory.clothing]?.length, 2); expect(bc[PackingCategory.electronics]?.length, 1);
    });

    test('toJson/fromJson roundtrip', () {
      final r = PackingList.fromJson(PackingList(
        id: 'l1', name: 'Beach Trip', tripType: TripType.beach,
        departureDate: DateTime(2026, 6, 1), returnDate: DateTime(2026, 6, 8), destination: 'Hawaii',
        items: [item('1', 'Sunscreen', PackingCategory.toiletries, essential: true)], createdAt: DateTime(2026, 1, 1),
      ).toJson());
      expect(r.id, 'l1'); expect(r.name, 'Beach Trip'); expect(r.tripType, TripType.beach);
      expect(r.destination, 'Hawaii'); expect(r.items.length, 1);
    });
  });

  group('PackingListService', () {
    test('getTemplateItems returns non-empty for all trip types', () {
      for (final t in TripType.values) expect(service.getTemplateItems(t), isNotEmpty, reason: '${t.label}');
    });

    test('beach template has swimsuit and sunscreen', () {
      final items = service.getTemplateItems(TripType.beach);
      expect(items.any((i) => i.name.toLowerCase().contains('swimsuit')), true);
      expect(items.any((i) => i.name.toLowerCase().contains('sunscreen')), true);
    });

    test('business template has laptop', () {
      expect(service.getTemplateItems(TripType.business).any((i) => i.name.toLowerCase().contains('laptop')), true);
    });

    test('camping template has tent', () {
      expect(service.getTemplateItems(TripType.camping).any((i) => i.name.toLowerCase().contains('tent')), true);
    });

    test('computeStats empty', () {
      final s = service.computeStats([]); expect(s.totalLists, 0); expect(s.overallProgress, 0);
    });

    test('computeStats multiple lists', () {
      final s = service.computeStats([
        makeList('l1', 'T1', [item('1', 'A', PackingCategory.clothing, packed: true), item('2', 'B', PackingCategory.clothing)]),
        makeList('l2', 'T2', [item('3', 'C', PackingCategory.electronics, packed: true), item('4', 'D', PackingCategory.electronics, packed: true)]),
      ]);
      expect(s.totalLists, 2); expect(s.completedLists, 1); expect(s.totalItems, 4); expect(s.packedItems, 3); expect(s.overallProgress, 75.0);
    });

    test('findCommonlyForgotten', () {
      final f = service.findCommonlyForgotten([
        makeList('l1', 'T1', [item('1', 'Passport', PackingCategory.documents, essential: true), item('2', 'Charger', PackingCategory.electronics, essential: true, packed: true)]),
        makeList('l2', 'T2', [item('3', 'Passport', PackingCategory.documents, essential: true), item('4', 'Toothbrush', PackingCategory.toiletries, essential: true)]),
      ]);
      expect(f.first, 'Passport');
    });

    test('findCommonlyForgotten empty when all packed', () {
      expect(service.findCommonlyForgotten([makeList('l1', 'T', [item('1', 'A', PackingCategory.clothing, essential: true, packed: true)])]), isEmpty);
    });

    test('mostUnpackedCategory', () {
      expect(service.mostUnpackedCategory(makeList('l1', 'T', [
        item('1', 'A', PackingCategory.clothing), item('2', 'B', PackingCategory.clothing),
        item('3', 'C', PackingCategory.electronics), item('4', 'D', PackingCategory.electronics, packed: true),
      ])), PackingCategory.clothing);
    });

    test('mostUnpackedCategory null', () {
      expect(service.mostUnpackedCategory(makeList('l1', 'T', [])), isNull);
      expect(service.mostUnpackedCategory(makeList('l1', 'T', [item('1', 'A', PackingCategory.clothing, packed: true)])), isNull);
    });

    test('prioritizedUnpacked essentials first', () {
      final p = service.prioritizedUnpacked(makeList('l1', 'T', [
        item('1', 'Hat', PackingCategory.accessories),
        item('2', 'Passport', PackingCategory.documents, essential: true),
        item('3', 'Shirt', PackingCategory.clothing, packed: true),
      ]));
      expect(p.length, 2); expect(p.first.name, 'Passport');
    });

    test('suggestQuantities scales', () {
      final s = service.suggestQuantities(3); final l = service.suggestQuantities(10);
      expect(l['Underwear']!, greaterThan(s['Underwear']!));
    });
  });

  group('PackingStats', () {
    test('progress calculations', () {
      const s = PackingStats(totalLists: 2, completedLists: 1, totalItems: 10, packedItems: 7, essentialItems: 4, packedEssentials: 3, categoryBreakdown: {});
      expect(s.overallProgress, 70.0); expect(s.essentialProgress, 75.0); expect(s.unpackedItems, 3); expect(s.unpackedEssentials, 1);
    });

    test('topCategory', () {
      const s = PackingStats(totalLists: 1, completedLists: 0, totalItems: 5, packedItems: 2, essentialItems: 1, packedEssentials: 0,
        categoryBreakdown: {PackingCategory.clothing: 3, PackingCategory.electronics: 2});
      expect(s.topCategory, PackingCategory.clothing);
    });

    test('topCategory null', () {
      const s = PackingStats(totalLists: 0, completedLists: 0, totalItems: 0, packedItems: 0, essentialItems: 0, packedEssentials: 0, categoryBreakdown: {});
      expect(s.topCategory, isNull);
    });
  });
}
