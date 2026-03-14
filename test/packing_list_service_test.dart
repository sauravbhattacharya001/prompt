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
      for (final c in PackingCategory.values) {
        expect(c.label, isNotEmpty);
        expect(c.emoji, isNotEmpty);
      }
    });
  });

  group('TripType', () {
    test('all values have label', () {
      for (final t in TripType.values) {
        expect(t.label, isNotEmpty);
      }
    });
  });

  group('PackingItem', () {
    test('defaults', () {
      final i = item('1', 'Shirt', PackingCategory.clothing);
      expect(i.quantity, 1);
      expect(i.isPacked, false);
      expect(i.isEssential, false);
      expect(i.notes, isNull);
    });

    test('copyWith', () {
      final packed = item('1', 'Shirt', PackingCategory.clothing).copyWith(isPacked: true);
      expect(packed.isPacked, true);
      expect(packed.name, 'Shirt');
      expect(packed.id, '1');
    });

    test('toJson/fromJson roundtrip', () {
      final i = PackingItem(id: 'x1', name: 'Laptop', category: PackingCategory.electronics,
        quantity: 1, isPacked: true, isEssential: true, notes: 'Work laptop');
      final restored = PackingItem.fromJson(i.toJson());
      expect(restored.id, 'x1');
      expect(restored.name, 'Laptop');
      expect(restored.category, PackingCategory.electronics);
      expect(restored.isPacked, true);
      expect(restored.isEssential, true);
      expect(restored.notes, 'Work laptop');
    });
  });

  group('PackingList', () {
    test('counts packed/unpacked items', () {
      final list = makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing, packed: true),
        item('2', 'B', PackingCategory.clothing),
        item('3', 'C', PackingCategory.clothing, packed: true),
      ]);
      expect(list.totalItems, 3);
      expect(list.packedItems, 2);
      expect(list.unpackedItems, 1);
    });

    test('tracks essential items', () {
      final list = makeList('l1', 'Trip', [
        item('1', 'Passport', PackingCategory.documents, essential: true, packed: true),
        item('2', 'Charger', PackingCategory.electronics, essential: true),
        item('3', 'Hat', PackingCategory.accessories),
      ]);
      expect(list.essentialItems, 2);
      expect(list.packedEssentials, 1);
      expect(list.unpackedEssentials, 1);
    });

    test('progress percent', () {
      expect(makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing, packed: true),
        item('2', 'B', PackingCategory.clothing),
      ]).progressPercent, 50.0);
      expect(makeList('l1', 'Trip', []).progressPercent, 0);
    });

    test('isComplete', () {
      expect(makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing, packed: true),
        item('2', 'B', PackingCategory.clothing, packed: true),
      ]).isComplete, true);
      expect(makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing, packed: true),
        item('2', 'B', PackingCategory.clothing),
      ]).isComplete, false);
      expect(makeList('l1', 'Trip', []).isComplete, false);
    });

    test('daysUntilDeparture', () {
      expect(makeList('l1', 'Trip', [], departure: now.add(const Duration(days: 5)))
        .daysUntilDeparture, greaterThanOrEqualTo(4));
      expect(makeList('l1', 'Trip', []).daysUntilDeparture, isNull);
    });

    test('tripDurationDays', () {
      expect(makeList('l1', 'Trip', [],
        departure: DateTime(2026, 3, 1), returnDate: DateTime(2026, 3, 8)).tripDurationDays, 7);
      expect(makeList('l1', 'Trip', []).tripDurationDays, isNull);
    });

    test('itemsByCategory groups correctly', () {
      final byCategory = makeList('l1', 'Trip', [
        item('1', 'Shirt', PackingCategory.clothing),
        item('2', 'Pants', PackingCategory.clothing),
        item('3', 'Charger', PackingCategory.electronics),
      ]).itemsByCategory;
      expect(byCategory[PackingCategory.clothing]?.length, 2);
      expect(byCategory[PackingCategory.electronics]?.length, 1);
    });

    test('toJson/fromJson roundtrip', () {
      final restored = PackingList.fromJson(PackingList(
        id: 'l1', name: 'Beach Trip', tripType: TripType.beach,
        departureDate: DateTime(2026, 6, 1), returnDate: DateTime(2026, 6, 8),
        destination: 'Hawaii',
        items: [item('1', 'Sunscreen', PackingCategory.toiletries, essential: true)],
        createdAt: DateTime(2026, 1, 1),
      ).toJson());
      expect(restored.id, 'l1');
      expect(restored.name, 'Beach Trip');
      expect(restored.tripType, TripType.beach);
      expect(restored.destination, 'Hawaii');
      expect(restored.items.length, 1);
    });
  });

  group('PackingListService', () {
    test('getTemplateItems returns non-empty for all trip types', () {
      for (final type in TripType.values) {
        expect(service.getTemplateItems(type), isNotEmpty, reason: '${type.label} template');
      }
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

    test('computeStats with empty list', () {
      final stats = service.computeStats([]);
      expect(stats.totalLists, 0);
      expect(stats.totalItems, 0);
      expect(stats.overallProgress, 0);
    });

    test('computeStats with multiple lists', () {
      final stats = service.computeStats([
        makeList('l1', 'Trip 1', [item('1', 'A', PackingCategory.clothing, packed: true), item('2', 'B', PackingCategory.clothing)]),
        makeList('l2', 'Trip 2', [item('3', 'C', PackingCategory.electronics, packed: true), item('4', 'D', PackingCategory.electronics, packed: true)]),
      ]);
      expect(stats.totalLists, 2);
      expect(stats.completedLists, 1);
      expect(stats.totalItems, 4);
      expect(stats.packedItems, 3);
      expect(stats.overallProgress, 75.0);
    });

    test('findCommonlyForgotten identifies patterns', () {
      final forgotten = service.findCommonlyForgotten([
        makeList('l1', 'Trip 1', [item('1', 'Passport', PackingCategory.documents, essential: true), item('2', 'Charger', PackingCategory.electronics, essential: true, packed: true)]),
        makeList('l2', 'Trip 2', [item('3', 'Passport', PackingCategory.documents, essential: true), item('4', 'Toothbrush', PackingCategory.toiletries, essential: true)]),
      ]);
      expect(forgotten.first, 'Passport');
    });

    test('findCommonlyForgotten empty for fully packed', () {
      expect(service.findCommonlyForgotten([
        makeList('l1', 'Trip', [item('1', 'A', PackingCategory.clothing, essential: true, packed: true)]),
      ]), isEmpty);
    });

    test('mostUnpackedCategory', () {
      expect(service.mostUnpackedCategory(makeList('l1', 'Trip', [
        item('1', 'A', PackingCategory.clothing), item('2', 'B', PackingCategory.clothing),
        item('3', 'C', PackingCategory.electronics), item('4', 'D', PackingCategory.electronics, packed: true),
      ])), PackingCategory.clothing);
    });

    test('mostUnpackedCategory null for empty/all-packed', () {
      expect(service.mostUnpackedCategory(makeList('l1', 'Trip', [])), isNull);
      expect(service.mostUnpackedCategory(makeList('l1', 'Trip', [item('1', 'A', PackingCategory.clothing, packed: true)])), isNull);
    });

    test('prioritizedUnpacked puts essentials first', () {
      final prioritized = service.prioritizedUnpacked(makeList('l1', 'Trip', [
        item('1', 'Hat', PackingCategory.accessories),
        item('2', 'Passport', PackingCategory.documents, essential: true),
        item('3', 'Shirt', PackingCategory.clothing, packed: true),
      ]));
      expect(prioritized.length, 2);
      expect(prioritized.first.name, 'Passport');
    });

    test('suggestQuantities scales with trip days', () {
      final short = service.suggestQuantities(3);
      final long = service.suggestQuantities(10);
      expect(long['Underwear']!, greaterThan(short['Underwear']!));
      expect(long['Socks']!, greaterThan(short['Socks']!));
    });
  });

  group('PackingStats', () {
    test('overallProgress and essentialProgress', () {
      const stats = PackingStats(totalLists: 2, completedLists: 1, totalItems: 10, packedItems: 7,
        essentialItems: 4, packedEssentials: 3, categoryBreakdown: {});
      expect(stats.overallProgress, 70.0);
      expect(stats.essentialProgress, 75.0);
      expect(stats.unpackedItems, 3);
      expect(stats.unpackedEssentials, 1);
    });

    test('topCategory', () {
      const stats = PackingStats(totalLists: 1, completedLists: 0, totalItems: 5, packedItems: 2,
        essentialItems: 1, packedEssentials: 0,
        categoryBreakdown: {PackingCategory.clothing: 3, PackingCategory.electronics: 2});
      expect(stats.topCategory, PackingCategory.clothing);
    });

    test('topCategory null for empty breakdown', () {
      const stats = PackingStats(totalLists: 0, completedLists: 0, totalItems: 0, packedItems: 0,
        essentialItems: 0, packedEssentials: 0, categoryBreakdown: {});
      expect(stats.topCategory, isNull);
    });
  });
}
